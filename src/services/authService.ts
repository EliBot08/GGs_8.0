/**
 * Enterprise Authentication Service
 * Handles secure authentication, session management, and role-based access control
 */

import { httpClient, ApiError } from './http';

export interface LoginRequest {
  username: string;
  password: string;
  rememberMe?: boolean;
}

export interface LoginResponse {
  accessToken: string;
  refreshToken?: string;
  expiresAt: string;
  user: {
    id: string;
    email: string;
    roles: string[];
    permissions?: string[];
  };
}

export interface RefreshRequest {
  refreshToken: string;
}

export interface UserInfo {
  id: string;
  email: string;
  userName: string;
  roles: string[];
  permissions: string[];
  lastLoginUtc?: string;
  emailConfirmed: boolean;
  lockoutEnd?: string;
  isLockedOut: boolean;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface ForgotPasswordRequest {
  email: string;
}

export interface ResetPasswordRequest {
  email: string;
  token: string;
  newPassword: string;
}

export interface TwoFactorSetupResponse {
  qrCodeUri: string;
  secret: string;
  recoveryCodes: string[];
}

export interface TwoFactorVerifyRequest {
  code: string;
}

export class AuthService {
  private currentUser: UserInfo | null = null;
  private tokenExpiryTimer: NodeJS.Timeout | null = null;
  private authStateListeners: Array<(isAuthenticated: boolean, user?: UserInfo) => void> = [];

  constructor() {
    this.initializeAuthState();
  }

  private initializeAuthState(): void {
    // Check if we have a valid session on startup
    if (httpClient.isAuthenticated()) {
      this.getCurrentUser()
        .then(user => {
          this.currentUser = user;
          this.notifyAuthStateChange(true, user);
          this.scheduleTokenRefresh();
        })
        .catch(() => {
          // Invalid token, clear it
          httpClient.clearTokens();
          this.notifyAuthStateChange(false);
        });
    }
  }

  public onAuthStateChange(listener: (isAuthenticated: boolean, user?: UserInfo) => void): () => void {
    this.authStateListeners.push(listener);
    
    // Return unsubscribe function
    return () => {
      const index = this.authStateListeners.indexOf(listener);
      if (index > -1) {
        this.authStateListeners.splice(index, 1);
      }
    };
  }

  private notifyAuthStateChange(isAuthenticated: boolean, user?: UserInfo): void {
    this.authStateListeners.forEach(listener => {
      try {
        listener(isAuthenticated, user);
      } catch (error) {
        console.error('Error in auth state listener:', error);
      }
    });
  }

  private scheduleTokenRefresh(): void {
    if (this.tokenExpiryTimer) {
      clearTimeout(this.tokenExpiryTimer);
    }

    // Refresh token 5 minutes before expiry
    const refreshDelay = 25 * 60 * 1000; // 25 minutes (assuming 30min token lifetime)
    
    this.tokenExpiryTimer = setTimeout(async () => {
      try {
        await this.refreshTokens();
      } catch (error) {
        console.error('Automatic token refresh failed:', error);
        await this.logout();
      }
    }, refreshDelay);
  }

  public async login(credentials: LoginRequest): Promise<LoginResponse> {
    try {
      const response = await httpClient.post<LoginResponse>('/api/auth/login', credentials);
      
      // Store tokens
      httpClient.setTokens(response.data.accessToken, response.data.refreshToken);
      
      // Get user information
      this.currentUser = await this.getCurrentUser();
      
      // Schedule token refresh
      this.scheduleTokenRefresh();
      
      // Notify listeners
      this.notifyAuthStateChange(true, this.currentUser);
      
      return response.data;
    } catch (error) {
      if (error instanceof ApiError) {
        // Handle specific authentication errors
        if (error.status === 401) {
          throw new Error('Invalid username or password');
        } else if (error.status === 423) {
          throw new Error('Account is locked. Please contact support.');
        } else if (error.problemDetails?.detail) {
          throw new Error(error.problemDetails.detail);
        }
      }
      throw new Error('Login failed. Please try again.');
    }
  }

  public async logout(): Promise<void> {
    try {
      // Attempt to revoke tokens on server
      await httpClient.post('/api/auth/logout');
    } catch (error) {
      // Continue with logout even if server call fails
      console.warn('Server logout failed:', error);
    }

    // Clear local state
    httpClient.clearTokens();
    this.currentUser = null;
    
    if (this.tokenExpiryTimer) {
      clearTimeout(this.tokenExpiryTimer);
      this.tokenExpiryTimer = null;
    }

    // Notify listeners
    this.notifyAuthStateChange(false);
  }

  public async refreshTokens(): Promise<void> {
    try {
      const response = await httpClient.post<LoginResponse>('/api/auth/refresh');
      
      // Update tokens
      httpClient.setTokens(response.data.accessToken, response.data.refreshToken);
      
      // Update user info
      this.currentUser = response.data.user;
      
      // Schedule next refresh
      this.scheduleTokenRefresh();
      
      // Notify listeners
      this.notifyAuthStateChange(true, this.currentUser);
    } catch (error) {
      console.error('Token refresh failed:', error);
      await this.logout();
      throw error;
    }
  }

  public async getCurrentUser(): Promise<UserInfo> {
    try {
      const response = await httpClient.get<UserInfo>('/api/auth/me');
      return response.data;
    } catch (error) {
      throw new Error('Failed to get current user information');
    }
  }

  public async changePassword(request: ChangePasswordRequest): Promise<void> {
    try {
      await httpClient.post('/api/auth/change-password', request);
    } catch (error) {
      if (error instanceof ApiError && error.problemDetails?.detail) {
        throw new Error(error.problemDetails.detail);
      }
      throw new Error('Failed to change password');
    }
  }

  public async forgotPassword(request: ForgotPasswordRequest): Promise<void> {
    try {
      await httpClient.post('/api/auth/forgot-password', request);
    } catch (error) {
      if (error instanceof ApiError && error.problemDetails?.detail) {
        throw new Error(error.problemDetails.detail);
      }
      throw new Error('Failed to send password reset email');
    }
  }

  public async resetPassword(request: ResetPasswordRequest): Promise<void> {
    try {
      await httpClient.post('/api/auth/reset-password', request);
    } catch (error) {
      if (error instanceof ApiError && error.problemDetails?.detail) {
        throw new Error(error.problemDetails.detail);
      }
      throw new Error('Failed to reset password');
    }
  }

  public async setupTwoFactor(): Promise<TwoFactorSetupResponse> {
    try {
      const response = await httpClient.post<TwoFactorSetupResponse>('/api/auth/2fa/setup');
      return response.data;
    } catch (error) {
      throw new Error('Failed to setup two-factor authentication');
    }
  }

  public async verifyTwoFactor(request: TwoFactorVerifyRequest): Promise<void> {
    try {
      await httpClient.post('/api/auth/2fa/verify', request);
    } catch (error) {
      if (error instanceof ApiError && error.problemDetails?.detail) {
        throw new Error(error.problemDetails.detail);
      }
      throw new Error('Failed to verify two-factor code');
    }
  }

  public async disableTwoFactor(): Promise<void> {
    try {
      await httpClient.post('/api/auth/2fa/disable');
    } catch (error) {
      throw new Error('Failed to disable two-factor authentication');
    }
  }

  public async generateRecoveryCodes(): Promise<string[]> {
    try {
      const response = await httpClient.post<{ codes: string[] }>('/api/auth/2fa/recovery-codes');
      return response.data.codes;
    } catch (error) {
      throw new Error('Failed to generate recovery codes');
    }
  }

  // Getters
  public get user(): UserInfo | null {
    return this.currentUser;
  }

  public get isAuthenticated(): boolean {
    return !!this.currentUser && httpClient.isAuthenticated();
  }

  public get userRoles(): string[] {
    return this.currentUser?.roles || [];
  }

  public get userPermissions(): string[] {
    return this.currentUser?.permissions || [];
  }

  // Role and permission checking
  public hasRole(role: string): boolean {
    return this.userRoles.includes(role);
  }

  public hasAnyRole(roles: string[]): boolean {
    return roles.some(role => this.hasRole(role));
  }

  public hasAllRoles(roles: string[]): boolean {
    return roles.every(role => this.hasRole(role));
  }

  public hasPermission(permission: string): boolean {
    return this.userPermissions.includes(permission);
  }

  public hasAnyPermission(permissions: string[]): boolean {
    return permissions.some(permission => this.hasPermission(permission));
  }

  public hasAllPermissions(permissions: string[]): boolean {
    return permissions.every(permission => this.hasPermission(permission));
  }

  public isAdmin(): boolean {
    return this.hasRole('Admin') || this.hasRole('Owner');
  }

  public isManager(): boolean {
    return this.hasAnyRole(['Admin', 'Owner', 'Manager']);
  }

  public canManageUsers(): boolean {
    return this.hasPermission('ManageUsers') || this.isAdmin();
  }

  public canManageLicenses(): boolean {
    return this.hasPermission('ManageLicenses') || this.isAdmin();
  }

  public canViewAnalytics(): boolean {
    return this.hasPermission('ViewAnalytics') || this.hasAnyRole(['Admin', 'Owner', 'Manager', 'Support']);
  }

  public canExecuteRemote(): boolean {
    return this.hasPermission('ExecuteRemote') || this.hasAnyRole(['Admin', 'Owner', 'Manager']);
  }
}

// Global auth service instance
export const authService = new AuthService();

// Utility function for protected routes
export const requireAuth = (): UserInfo => {
  if (!authService.isAuthenticated || !authService.user) {
    throw new Error('Authentication required');
  }
  return authService.user;
};

// Utility function for role checking in components
export const requireRole = (roles: string | string[]): UserInfo => {
  const user = requireAuth();
  const roleArray = Array.isArray(roles) ? roles : [roles];
  
  if (!authService.hasAnyRole(roleArray)) {
    throw new Error(`Required role(s): ${roleArray.join(', ')}`);
  }
  
  return user;
};

// Utility function for permission checking in components
export const requirePermission = (permissions: string | string[]): UserInfo => {
  const user = requireAuth();
  const permissionArray = Array.isArray(permissions) ? permissions : [permissions];
  
  if (!authService.hasAnyPermission(permissionArray)) {
    throw new Error(`Required permission(s): ${permissionArray.join(', ')}`);
  }
  
  return user;
};
