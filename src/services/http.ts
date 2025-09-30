/**
 * Enterprise HTTP Client Service
 * Provides secure, enterprise-grade HTTP communication with the GGs server
 * Includes automatic authentication, retry logic, and proper error handling
 */

export interface ApiResponse<T = any> {
  data: T;
  status: number;
  statusText: string;
  headers: Record<string, string>;
}

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  errors?: Record<string, string[]>;
}

export class ApiError extends Error {
  constructor(
    public status: number,
    public statusText: string,
    public problemDetails?: ProblemDetails,
    public response?: Response
  ) {
    super(problemDetails?.detail || problemDetails?.title || statusText);
    this.name = 'ApiError';
  }
}

export interface HttpClientConfig {
  baseURL: string;
  timeout: number;
  retryAttempts: number;
  retryDelay: number;
  defaultHeaders: Record<string, string>;
}

export class HttpClient {
  private config: HttpClientConfig;
  private accessToken: string | null = null;
  private refreshToken: string | null = null;
  private refreshPromise: Promise<void> | null = null;

  constructor(config?: Partial<HttpClientConfig>) {
    this.config = {
      baseURL: this.getBaseURL(),
      timeout: 30000,
      retryAttempts: 3,
      retryDelay: 1000,
      defaultHeaders: {
        'Content-Type': 'application/json',
        'Accept': 'application/json',
        'User-Agent': 'GGs.WebAdmin/1.0'
      },
      ...config
    };

    // Load tokens from storage
    this.loadTokensFromStorage();
  }

  private getBaseURL(): string {
    // Check for runtime environment variable first
    if (typeof window !== 'undefined' && (window as any).env?.API_BASE_URL) {
      return (window as any).env.API_BASE_URL;
    }
    
    // Fallback to process.env for build-time configuration
    if (process.env.REACT_APP_API_BASE_URL) {
      return process.env.REACT_APP_API_BASE_URL;
    }
    
    // Development fallback
    return 'https://localhost:5001';
  }

  private loadTokensFromStorage(): void {
    try {
      this.accessToken = sessionStorage.getItem('ggs_access_token');
      this.refreshToken = sessionStorage.getItem('ggs_refresh_token');
    } catch (error) {
      console.warn('Failed to load tokens from storage:', error);
    }
  }

  private saveTokensToStorage(): void {
    try {
      if (this.accessToken) {
        sessionStorage.setItem('ggs_access_token', this.accessToken);
      } else {
        sessionStorage.removeItem('ggs_access_token');
      }

      if (this.refreshToken) {
        sessionStorage.setItem('ggs_refresh_token', this.refreshToken);
      } else {
        sessionStorage.removeItem('ggs_refresh_token');
      }
    } catch (error) {
      console.warn('Failed to save tokens to storage:', error);
    }
  }

  public setTokens(accessToken: string, refreshToken?: string): void {
    this.accessToken = accessToken;
    if (refreshToken) {
      this.refreshToken = refreshToken;
    }
    this.saveTokensToStorage();
  }

  public clearTokens(): void {
    this.accessToken = null;
    this.refreshToken = null;
    this.saveTokensToStorage();
  }

  public isAuthenticated(): boolean {
    return !!this.accessToken;
  }

  private generateCorrelationId(): string {
    return `web-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
  }

  private async refreshAccessToken(): Promise<void> {
    if (!this.refreshToken) {
      throw new ApiError(401, 'Unauthorized', { detail: 'No refresh token available' });
    }

    // Prevent multiple simultaneous refresh attempts
    if (this.refreshPromise) {
      return this.refreshPromise;
    }

    this.refreshPromise = this.performTokenRefresh();
    
    try {
      await this.refreshPromise;
    } finally {
      this.refreshPromise = null;
    }
  }

  private async performTokenRefresh(): Promise<void> {
    try {
      const response = await fetch(`${this.config.baseURL}/api/auth/refresh`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'X-Correlation-ID': this.generateCorrelationId()
        },
        body: JSON.stringify({ refreshToken: this.refreshToken })
      });

      if (!response.ok) {
        throw new ApiError(response.status, response.statusText);
      }

      const data = await response.json();
      this.setTokens(data.accessToken, data.refreshToken);
    } catch (error) {
      // Clear invalid tokens
      this.clearTokens();
      throw error;
    }
  }

  private async buildRequest(
    url: string,
    options: RequestInit = {}
  ): Promise<Request> {
    const fullUrl = url.startsWith('http') ? url : `${this.config.baseURL}${url.startsWith('/') ? url : `/${url}`}`;
    
    const headers = new Headers({
      ...this.config.defaultHeaders,
      ...options.headers,
      'X-Correlation-ID': this.generateCorrelationId()
    });

    // Add authentication header if available
    if (this.accessToken) {
      headers.set('Authorization', `Bearer ${this.accessToken}`);
    }

    return new Request(fullUrl, {
      ...options,
      headers
    });
  }

  private async executeRequest<T = any>(
    request: Request,
    attempt: number = 1
  ): Promise<ApiResponse<T>> {
    try {
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), this.config.timeout);

      const response = await fetch(request.clone(), {
        signal: controller.signal
      });

      clearTimeout(timeoutId);

      // Handle 401 with automatic token refresh
      if (response.status === 401 && this.refreshToken && attempt === 1) {
        await this.refreshAccessToken();
        
        // Retry with new token
        const newRequest = await this.buildRequest(request.url, {
          method: request.method,
          headers: Object.fromEntries(request.headers.entries()),
          body: request.body
        });
        
        return this.executeRequest<T>(newRequest, 2);
      }

      // Parse response
      const responseHeaders: Record<string, string> = {};
      response.headers.forEach((value, key) => {
        responseHeaders[key] = value;
      });

      let data: T;
      const contentType = response.headers.get('content-type') || '';
      
      if (contentType.includes('application/json')) {
        data = await response.json();
      } else {
        data = await response.text() as any;
      }

      if (!response.ok) {
        // Handle ProblemDetails response format
        const problemDetails = contentType.includes('application/json') && 
          typeof data === 'object' && 
          data !== null &&
          ('title' in data || 'detail' in data) ? data as ProblemDetails : undefined;

        throw new ApiError(response.status, response.statusText, problemDetails, response);
      }

      return {
        data,
        status: response.status,
        statusText: response.statusText,
        headers: responseHeaders
      };

    } catch (error) {
      if (error instanceof ApiError) {
        throw error;
      }

      // Handle network errors and timeouts with retry logic
      if (attempt < this.config.retryAttempts && 
          (error instanceof TypeError || error.name === 'AbortError')) {
        
        console.warn(`Request failed (attempt ${attempt}/${this.config.retryAttempts}):`, error.message);
        
        // Exponential backoff
        const delay = this.config.retryDelay * Math.pow(2, attempt - 1);
        await new Promise(resolve => setTimeout(resolve, delay));
        
        return this.executeRequest<T>(request, attempt + 1);
      }

      throw new ApiError(0, 'Network Error', { detail: error.message });
    }
  }

  public async get<T = any>(url: string, config?: RequestInit): Promise<ApiResponse<T>> {
    const request = await this.buildRequest(url, { ...config, method: 'GET' });
    return this.executeRequest<T>(request);
  }

  public async post<T = any>(url: string, data?: any, config?: RequestInit): Promise<ApiResponse<T>> {
    const body = data !== undefined ? JSON.stringify(data) : undefined;
    const request = await this.buildRequest(url, { ...config, method: 'POST', body });
    return this.executeRequest<T>(request);
  }

  public async put<T = any>(url: string, data?: any, config?: RequestInit): Promise<ApiResponse<T>> {
    const body = data !== undefined ? JSON.stringify(data) : undefined;
    const request = await this.buildRequest(url, { ...config, method: 'PUT', body });
    return this.executeRequest<T>(request);
  }

  public async patch<T = any>(url: string, data?: any, config?: RequestInit): Promise<ApiResponse<T>> {
    const body = data !== undefined ? JSON.stringify(data) : undefined;
    const request = await this.buildRequest(url, { ...config, method: 'PATCH', body });
    return this.executeRequest<T>(request);
  }

  public async delete<T = any>(url: string, config?: RequestInit): Promise<ApiResponse<T>> {
    const request = await this.buildRequest(url, { ...config, method: 'DELETE' });
    return this.executeRequest<T>(request);
  }

  public async upload<T = any>(url: string, formData: FormData, config?: RequestInit): Promise<ApiResponse<T>> {
    // Remove Content-Type header for multipart/form-data - browser will set it with boundary
    const headers = { ...config?.headers };
    delete (headers as any)['Content-Type'];

    const request = await this.buildRequest(url, {
      ...config,
      method: 'POST',
      body: formData,
      headers
    });

    return this.executeRequest<T>(request);
  }
}

// Global HTTP client instance
export const httpClient = new HttpClient();

// Export types for use in other services
export type { HttpClientConfig };
