/**
 * Enterprise License Management Service
 * Comprehensive license administration and enforcement for enterprise environments
 */

import { httpClient } from './http';

export interface License {
  id: string;
  licenseId: string;
  userId: string;
  userName?: string;
  userEmail?: string;
  tier: 'Basic' | 'Pro' | 'Enterprise' | 'Admin';
  status: 'Active' | 'Suspended' | 'Revoked' | 'Expired';
  issuedAt: string;
  expiresAt?: string;
  isAdminKey: boolean;
  deviceBindingId?: string;
  allowOfflineValidation: boolean;
  developerMode: boolean;
  maxDevices: number;
  usageCount: number;
  assignedDevices: string[];
  notes?: string;
  metadata?: Record<string, any>;
  lastValidated?: string;
  signedLicense: string;
}

export interface CreateLicenseRequest {
  userId: string;
  tier: 'Basic' | 'Pro' | 'Enterprise' | 'Admin';
  expiresAt?: string;
  isAdminKey?: boolean;
  deviceBindingId?: string;
  allowOfflineValidation?: boolean;
  developerMode?: boolean;
  maxDevices?: number;
  notes?: string;
  metadata?: Record<string, any>;
}

export interface UpdateLicenseRequest {
  status?: 'Active' | 'Suspended' | 'Revoked';
  expiresAt?: string;
  maxDevices?: number;
  developerMode?: boolean;
  allowOfflineValidation?: boolean;
  notes?: string;
  metadata?: Record<string, any>;
}

export interface LicenseValidationRequest {
  licenseKey: string;
  deviceId?: string;
  signature?: string;
}

export interface LicenseValidationResponse {
  isValid: boolean;
  license?: License;
  validationError?: string;
  remainingDevices?: number;
  expiresIn?: number; // days
  features: {
    maxTweaks: number;
    allowHighRisk: boolean;
    allowExperimental: boolean;
    allowRemoteExecution: boolean;
    allowBulkOperations: boolean;
    allowAnalytics: boolean;
    allowCloudProfiles: boolean;
    supportLevel: 'Basic' | 'Standard' | 'Premium' | 'Enterprise';
  };
}

export interface LicenseUsage {
  licenseId: string;
  deviceId: string;
  deviceName?: string;
  lastUsed: string;
  usageCount: number;
  features: Record<string, number>;
  location?: string;
  userAgent?: string;
}

export interface LicenseTemplate {
  id: string;
  name: string;
  description: string;
  tier: string;
  defaultExpirationDays: number;
  maxDevices: number;
  features: Record<string, boolean>;
  isActive: boolean;
  createdAt: string;
  usageCount: number;
}

export interface BulkLicenseOperation {
  licenseIds: string[];
  operation: 'activate' | 'suspend' | 'revoke' | 'extend' | 'transfer';
  parameters?: {
    newUserId?: string; // for transfer
    extensionDays?: number; // for extend
    reason?: string;
  };
}

export interface LicenseAnalytics {
  totalLicenses: number;
  activeLicenses: number;
  expiredLicenses: number;
  expiringIn30Days: number;
  utilizationRate: number;
  revenueImpact: number;
  byTier: Array<{
    tier: string;
    count: number;
    active: number;
    revenue: number;
    growth: number; // percentage change from last period
  }>;
  topUsers: Array<{
    userId: string;
    userName: string;
    licenseCount: number;
    totalUsage: number;
  }>;
  complianceStatus: {
    overusage: number;
    unlicensedDevices: number;
    riskScore: number;
  };
}

export interface LicenseFilters {
  search?: string;
  tiers?: string[];
  statuses?: string[];
  userIds?: string[];
  isAdminKey?: boolean;
  developerMode?: boolean;
  expiringWithinDays?: number;
  lastUsedAfter?: string;
  lastUsedBefore?: string;
  createdAfter?: string;
  createdBefore?: string;
}

export interface PaginatedResponse<T> {
  data: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export class LicenseService {
  public async getLicenses(
    page: number = 1,
    pageSize: number = 50,
    filters?: LicenseFilters
  ): Promise<PaginatedResponse<License>> {
    try {
      const params = new URLSearchParams();
      params.append('page', page.toString());
      params.append('pageSize', Math.min(pageSize, 100).toString());
      
      if (filters?.search) params.append('search', filters.search);
      if (filters?.tiers) filters.tiers.forEach(tier => params.append('tiers', tier));
      if (filters?.statuses) filters.statuses.forEach(status => params.append('statuses', status));
      if (filters?.userIds) filters.userIds.forEach(userId => params.append('userIds', userId));
      if (filters?.isAdminKey !== undefined) params.append('isAdminKey', filters.isAdminKey.toString());
      if (filters?.developerMode !== undefined) params.append('developerMode', filters.developerMode.toString());
      if (filters?.expiringWithinDays) params.append('expiringWithinDays', filters.expiringWithinDays.toString());
      if (filters?.lastUsedAfter) params.append('lastUsedAfter', filters.lastUsedAfter);
      if (filters?.lastUsedBefore) params.append('lastUsedBefore', filters.lastUsedBefore);
      if (filters?.createdAfter) params.append('createdAfter', filters.createdAfter);
      if (filters?.createdBefore) params.append('createdBefore', filters.createdBefore);

      const response = await httpClient.get<PaginatedResponse<License>>(`/api/v1/licenses?${params}`);
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch licenses');
    }
  }

  public async getLicenseById(licenseId: string): Promise<License> {
    try {
      const response = await httpClient.get<License>(`/api/v1/licenses/${licenseId}`);
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch license');
    }
  }

  public async createLicense(request: CreateLicenseRequest): Promise<License> {
    try {
      const response = await httpClient.post<License>('/api/v1/licenses/issue', request);
      return response.data;
    } catch (error) {
      throw new Error('Failed to create license');
    }
  }

  public async updateLicense(licenseId: string, request: UpdateLicenseRequest): Promise<License> {
    try {
      const response = await httpClient.put<License>(`/api/v1/licenses/${licenseId}`, request);
      return response.data;
    } catch (error) {
      throw new Error('Failed to update license');
    }
  }

  public async activateLicense(licenseId: string): Promise<void> {
    try {
      await httpClient.post(`/api/v1/licenses/${licenseId}/activate`);
    } catch (error) {
      throw new Error('Failed to activate license');
    }
  }

  public async suspendLicense(licenseId: string, reason?: string): Promise<void> {
    try {
      const body = reason ? { reason } : undefined;
      await httpClient.post(`/api/v1/licenses/${licenseId}/suspend`, body);
    } catch (error) {
      throw new Error('Failed to suspend license');
    }
  }

  public async revokeLicense(licenseId: string, reason?: string): Promise<void> {
    try {
      const body = reason ? { reason } : undefined;
      await httpClient.post(`/api/v1/licenses/${licenseId}/revoke`, body);
    } catch (error) {
      throw new Error('Failed to revoke license');
    }
  }

  public async extendLicense(licenseId: string, extensionDays: number): Promise<License> {
    try {
      const response = await httpClient.post<License>(`/api/v1/licenses/${licenseId}/extend`, {
        extensionDays
      });
      return response.data;
    } catch (error) {
      throw new Error('Failed to extend license');
    }
  }

  public async transferLicense(licenseId: string, newUserId: string, reason?: string): Promise<License> {
    try {
      const response = await httpClient.post<License>(`/api/v1/licenses/${licenseId}/transfer`, {
        newUserId,
        reason
      });
      return response.data;
    } catch (error) {
      throw new Error('Failed to transfer license');
    }
  }

  public async validateLicense(request: LicenseValidationRequest): Promise<LicenseValidationResponse> {
    try {
      const response = await httpClient.post<LicenseValidationResponse>('/api/v1/licenses/validate', request);
      return response.data;
    } catch (error) {
      throw new Error('Failed to validate license');
    }
  }

  public async getLicenseUsage(
    licenseId: string,
    page: number = 1,
    pageSize: number = 50
  ): Promise<PaginatedResponse<LicenseUsage>> {
    try {
      const params = new URLSearchParams();
      params.append('page', page.toString());
      params.append('pageSize', Math.min(pageSize, 100).toString());

      const response = await httpClient.get<PaginatedResponse<LicenseUsage>>(`/api/v1/licenses/${licenseId}/usage?${params}`);
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch license usage');
    }
  }

  public async getLicenseDevices(licenseId: string): Promise<Array<{
    deviceId: string;
    deviceName?: string;
    lastUsed: string;
    isOnline: boolean;
    registeredAt: string;
    location?: string;
    osVersion?: string;
    agentVersion?: string;
  }>> {
    try {
      const response = await httpClient.get(`/api/v1/licenses/${licenseId}/devices`);
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch license devices');
    }
  }

  public async removeLicenseDevice(licenseId: string, deviceId: string, reason?: string): Promise<void> {
    try {
      const body = reason ? { reason } : undefined;
      await httpClient.delete(`/api/v1/licenses/${licenseId}/devices/${deviceId}`, {
        body: body ? JSON.stringify(body) : undefined
      });
    } catch (error) {
      throw new Error('Failed to remove device from license');
    }
  }

  public async bulkLicenseOperation(operation: BulkLicenseOperation): Promise<{
    successful: Array<{ licenseId: string; message: string }>;
    failed: Array<{ licenseId: string; error: string }>;
    summary: {
      total: number;
      successful: number;
      failed: number;
    };
  }> {
    try {
      const response = await httpClient.post('/api/v1/licenses/bulk-operation', operation);
      return response.data;
    } catch (error) {
      throw new Error('Failed to perform bulk license operation');
    }
  }

  public async getLicenseAnalytics(): Promise<LicenseAnalytics> {
    try {
      const response = await httpClient.get<LicenseAnalytics>('/api/v1/licenses/analytics');
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch license analytics');
    }
  }

  public async getLicenseTemplates(): Promise<LicenseTemplate[]> {
    try {
      const response = await httpClient.get<LicenseTemplate[]>('/api/v1/licenses/templates');
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch license templates');
    }
  }

  public async createLicenseTemplate(template: Omit<LicenseTemplate, 'id' | 'createdAt' | 'usageCount'>): Promise<LicenseTemplate> {
    try {
      const response = await httpClient.post<LicenseTemplate>('/api/v1/licenses/templates', template);
      return response.data;
    } catch (error) {
      throw new Error('Failed to create license template');
    }
  }

  public async updateLicenseTemplate(templateId: string, template: Partial<LicenseTemplate>): Promise<LicenseTemplate> {
    try {
      const response = await httpClient.put<LicenseTemplate>(`/api/v1/licenses/templates/${templateId}`, template);
      return response.data;
    } catch (error) {
      throw new Error('Failed to update license template');
    }
  }

  public async deleteLicenseTemplate(templateId: string): Promise<void> {
    try {
      await httpClient.delete(`/api/v1/licenses/templates/${templateId}`);
    } catch (error) {
      throw new Error('Failed to delete license template');
    }
  }

  public async createLicenseFromTemplate(templateId: string, userId: string, customizations?: Partial<CreateLicenseRequest>): Promise<License> {
    try {
      const response = await httpClient.post<License>(`/api/v1/licenses/templates/${templateId}/create`, {
        userId,
        ...customizations
      });
      return response.data;
    } catch (error) {
      throw new Error('Failed to create license from template');
    }
  }

  public async getExpiringLicenses(days: number = 30): Promise<License[]> {
    try {
      const response = await httpClient.get<License[]>(`/api/v1/licenses/expiring?days=${days}`);
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch expiring licenses');
    }
  }

  public async getOverusageLicenses(): Promise<Array<License & {
    currentDevices: number;
    excessDevices: number;
    overusagePercentage: number;
  }>> {
    try {
      const response = await httpClient.get('/api/v1/licenses/overusage');
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch overusage licenses');
    }
  }

  public async downloadLicense(licenseId: string, format: 'json' | 'qr' | 'pdf'): Promise<Blob> {
    try {
      const response = await httpClient.get(`/api/v1/licenses/${licenseId}/download?format=${format}`, {
        headers: { 
          'Accept': format === 'pdf' ? 'application/pdf' : 
                   format === 'qr' ? 'image/png' : 'application/json'
        }
      });
      
      return new Blob([response.data], { 
        type: format === 'pdf' ? 'application/pdf' : 
              format === 'qr' ? 'image/png' : 'application/json'
      });
    } catch (error) {
      throw new Error('Failed to download license');
    }
  }

  public async exportLicenses(
    format: 'csv' | 'xlsx',
    filters?: LicenseFilters
  ): Promise<Blob> {
    try {
      const params = new URLSearchParams();
      params.append('format', format);
      
      if (filters?.search) params.append('search', filters.search);
      if (filters?.tiers) filters.tiers.forEach(tier => params.append('tiers', tier));
      if (filters?.statuses) filters.statuses.forEach(status => params.append('statuses', status));

      const response = await httpClient.get(`/api/v1/licenses/export?${params}`, {
        headers: { 
          'Accept': format === 'xlsx' ? 
            'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' : 
            'text/csv'
        }
      });
      
      return new Blob([response.data], { 
        type: format === 'xlsx' ? 
          'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' : 
          'text/csv'
      });
    } catch (error) {
      throw new Error('Failed to export licenses');
    }
  }

  public async getLicenseAuditLog(
    licenseId?: string,
    startDate?: string,
    endDate?: string,
    page: number = 1,
    pageSize: number = 50
  ): Promise<{
    entries: Array<{
      id: string;
      licenseId: string;
      action: string;
      details: Record<string, any>;
      performedBy: string;
      performedAt: string;
      ipAddress: string;
      userAgent: string;
    }>;
    totalCount: number;
    pageNumber: number;
    pageSize: number;
  }> {
    try {
      const params = new URLSearchParams();
      params.append('page', page.toString());
      params.append('pageSize', Math.min(pageSize, 100).toString());
      
      if (licenseId) params.append('licenseId', licenseId);
      if (startDate) params.append('startDate', startDate);
      if (endDate) params.append('endDate', endDate);

      const response = await httpClient.get(`/api/v1/licenses/audit?${params}`);
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch license audit log');
    }
  }

  // Utility methods
  public getTierDisplayName(tier: string): string {
    const displayNames: Record<string, string> = {
      'Basic': 'Basic License',
      'Pro': 'Professional License',
      'Enterprise': 'Enterprise License',
      'Admin': 'Administrator License'
    };
    
    return displayNames[tier] || tier;
  }

  public getTierFeatures(tier: string): {
    maxTweaks: number;
    allowHighRisk: boolean;
    allowExperimental: boolean;
    allowRemoteExecution: boolean;
    allowBulkOperations: boolean;
    allowAnalytics: boolean;
    allowCloudProfiles: boolean;
    supportLevel: string;
    maxDevices: number;
  } {
    const features: Record<string, any> = {
      'Basic': {
        maxTweaks: 10,
        allowHighRisk: false,
        allowExperimental: false,
        allowRemoteExecution: false,
        allowBulkOperations: false,
        allowAnalytics: false,
        allowCloudProfiles: false,
        supportLevel: 'Basic',
        maxDevices: 1
      },
      'Pro': {
        maxTweaks: 100,
        allowHighRisk: true,
        allowExperimental: false,
        allowRemoteExecution: true,
        allowBulkOperations: true,
        allowAnalytics: true,
        allowCloudProfiles: true,
        supportLevel: 'Standard',
        maxDevices: 5
      },
      'Enterprise': {
        maxTweaks: -1, // unlimited
        allowHighRisk: true,
        allowExperimental: true,
        allowRemoteExecution: true,
        allowBulkOperations: true,
        allowAnalytics: true,
        allowCloudProfiles: true,
        supportLevel: 'Premium',
        maxDevices: 50
      },
      'Admin': {
        maxTweaks: -1, // unlimited
        allowHighRisk: true,
        allowExperimental: true,
        allowRemoteExecution: true,
        allowBulkOperations: true,
        allowAnalytics: true,
        allowCloudProfiles: true,
        supportLevel: 'Enterprise',
        maxDevices: -1 // unlimited
      }
    };
    
    return features[tier] || features['Basic'];
  }

  public getStatusDisplayName(status: string): string {
    const displayNames: Record<string, string> = {
      'Active': 'Active',
      'Suspended': 'Suspended',
      'Revoked': 'Revoked',
      'Expired': 'Expired'
    };
    
    return displayNames[status] || status;
  }

  public getStatusColor(status: string): string {
    const colors: Record<string, string> = {
      'Active': '#28a745',
      'Suspended': '#ffc107',
      'Revoked': '#dc3545',
      'Expired': '#6c757d'
    };
    
    return colors[status] || '#6c757d';
  }

  public isLicenseExpiringSoon(license: License, days: number = 30): boolean {
    if (!license.expiresAt) return false;
    
    const expiryDate = new Date(license.expiresAt);
    const now = new Date();
    const daysUntilExpiry = Math.ceil((expiryDate.getTime() - now.getTime()) / (1000 * 60 * 60 * 24));
    
    return daysUntilExpiry <= days && daysUntilExpiry > 0;
  }

  public isLicenseExpired(license: License): boolean {
    if (!license.expiresAt) return false;
    
    const expiryDate = new Date(license.expiresAt);
    const now = new Date();
    
    return now > expiryDate;
  }

  public calculateUtilization(license: License): number {
    if (license.maxDevices === -1) return 0; // unlimited
    if (license.maxDevices === 0) return 100; // should not happen
    
    return Math.min((license.assignedDevices.length / license.maxDevices) * 100, 100);
  }

  public getLicenseRiskScore(license: License): number {
    let risk = 0;
    
    // Expired or expiring soon
    if (this.isLicenseExpired(license)) risk += 50;
    else if (this.isLicenseExpiringSoon(license, 7)) risk += 30;
    else if (this.isLicenseExpiringSoon(license, 30)) risk += 15;
    
    // Overutilization
    const utilization = this.calculateUtilization(license);
    if (utilization > 100) risk += 25;
    else if (utilization > 90) risk += 15;
    else if (utilization > 80) risk += 10;
    
    // High privilege license
    if (license.isAdminKey) risk += 20;
    if (license.tier === 'Enterprise' || license.tier === 'Admin') risk += 10;
    
    // Developer mode enabled
    if (license.developerMode) risk += 15;
    
    // Inactive usage
    const daysSinceLastValidation = license.lastValidated ? 
      (Date.now() - new Date(license.lastValidated).getTime()) / (1000 * 60 * 60 * 24) : 999;
    if (daysSinceLastValidation > 30) risk += 10;
    
    return Math.min(risk, 100);
  }
}

// Global license service instance
export const licenseService = new LicenseService();
