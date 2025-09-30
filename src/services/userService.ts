/**
 * Enterprise User Management Service
 * Comprehensive user administration for enterprise environments
 */

import { httpClient } from './http';

export interface User {
  id: string;
  userName: string;
  email: string;
  emailConfirmed: boolean;
  phoneNumber?: string;
  phoneNumberConfirmed: boolean;
  twoFactorEnabled: boolean;
  lockoutEnd?: string;
  lockoutEnabled: boolean;
  accessFailedCount: number;
  roles: string[];
  createdAt: string;
  lastLoginAt?: string;
  isActive: boolean;
  metadata?: Record<string, any>;
  department?: string;
  manager?: string;
  location?: string;
  jobTitle?: string;
}

export interface CreateUserRequest {
  userName: string;
  email: string;
  password: string;
  roles: string[];
  emailConfirmed?: boolean;
  requirePasswordChange?: boolean;
  department?: string;
  manager?: string;
  location?: string;
  jobTitle?: string;
  metadata?: Record<string, any>;
}

export interface UpdateUserRequest {
  userName?: string;
  email?: string;
  phoneNumber?: string;
  roles?: string[];
  department?: string;
  manager?: string;
  location?: string;
  jobTitle?: string;
  metadata?: Record<string, any>;
}

export interface ChangeUserPasswordRequest {
  userId: string;
  newPassword: string;
  requirePasswordChange: boolean;
}

export interface UserRoleChangeRequest {
  roles: string[];
  reason?: string;
}

export interface BulkUserOperation {
  userIds: string[];
  operation: 'activate' | 'deactivate' | 'delete' | 'unlock' | 'requirePasswordChange';
  reason?: string;
}

export interface UserImportRequest {
  users: Array<{
    userName: string;
    email: string;
    password: string;
    roles: string[];
    department?: string;
    manager?: string;
    location?: string;
    jobTitle?: string;
  }>;
  sendWelcomeEmails: boolean;
  requirePasswordChange: boolean;
}

export interface UserImportResult {
  successful: Array<{
    email: string;
    userId: string;
    message: string;
  }>;
  failed: Array<{
    email: string;
    error: string;
    rowNumber: number;
  }>;
  summary: {
    total: number;
    successful: number;
    failed: number;
  };
}

export interface UserActivity {
  userId: string;
  activity: string;
  timestamp: string;
  ipAddress: string;
  userAgent: string;
  success: boolean;
  details?: Record<string, any>;
}

export interface UserStats {
  userId: string;
  totalLogins: number;
  lastLogin?: string;
  tweaksExecuted: number;
  devicesManaged: number;
  licenseType: string;
  accountAge: number; // days
  riskScore: number; // 0-100
  privilegeLevel: 'Standard' | 'Elevated' | 'Administrative';
}

export interface UserFilters {
  search?: string;
  roles?: string[];
  departments?: string[];
  isActive?: boolean;
  emailConfirmed?: boolean;
  twoFactorEnabled?: boolean;
  lastLoginAfter?: string;
  lastLoginBefore?: string;
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

export class UserService {
  public async getUsers(
    page: number = 1,
    pageSize: number = 50,
    filters?: UserFilters
  ): Promise<PaginatedResponse<User>> {
    try {
      const params = new URLSearchParams();
      params.append('page', page.toString());
      params.append('pageSize', Math.min(pageSize, 100).toString());
      
      if (filters?.search) params.append('search', filters.search);
      if (filters?.roles) filters.roles.forEach(role => params.append('roles', role));
      if (filters?.departments) filters.departments.forEach(dept => params.append('departments', dept));
      if (filters?.isActive !== undefined) params.append('isActive', filters.isActive.toString());
      if (filters?.emailConfirmed !== undefined) params.append('emailConfirmed', filters.emailConfirmed.toString());
      if (filters?.twoFactorEnabled !== undefined) params.append('twoFactorEnabled', filters.twoFactorEnabled.toString());
      if (filters?.lastLoginAfter) params.append('lastLoginAfter', filters.lastLoginAfter);
      if (filters?.lastLoginBefore) params.append('lastLoginBefore', filters.lastLoginBefore);
      if (filters?.createdAfter) params.append('createdAfter', filters.createdAfter);
      if (filters?.createdBefore) params.append('createdBefore', filters.createdBefore);

      const response = await httpClient.get<PaginatedResponse<User>>(`/api/v1/users?${params}`);
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch users');
    }
  }

  public async getUserById(userId: string): Promise<User> {
    try {
      const response = await httpClient.get<User>(`/api/v1/users/${userId}`);
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch user');
    }
  }

  public async createUser(request: CreateUserRequest): Promise<User> {
    try {
      const response = await httpClient.post<User>('/api/v1/users', request);
      return response.data;
    } catch (error) {
      throw new Error('Failed to create user');
    }
  }

  public async updateUser(userId: string, request: UpdateUserRequest): Promise<User> {
    try {
      const response = await httpClient.put<User>(`/api/v1/users/${userId}`, request);
      return response.data;
    } catch (error) {
      throw new Error('Failed to update user');
    }
  }

  public async deleteUser(userId: string, reason?: string): Promise<void> {
    try {
      const body = reason ? { reason } : undefined;
      await httpClient.delete(`/api/v1/users/${userId}`, { body: JSON.stringify(body) });
    } catch (error) {
      throw new Error('Failed to delete user');
    }
  }

  public async activateUser(userId: string): Promise<void> {
    try {
      await httpClient.post(`/api/v1/users/${userId}/activate`);
    } catch (error) {
      throw new Error('Failed to activate user');
    }
  }

  public async deactivateUser(userId: string, reason?: string): Promise<void> {
    try {
      const body = reason ? { reason } : undefined;
      await httpClient.post(`/api/v1/users/${userId}/deactivate`, body);
    } catch (error) {
      throw new Error('Failed to deactivate user');
    }
  }

  public async unlockUser(userId: string): Promise<void> {
    try {
      await httpClient.post(`/api/v1/users/${userId}/unlock`);
    } catch (error) {
      throw new Error('Failed to unlock user');
    }
  }

  public async changeUserRoles(userId: string, request: UserRoleChangeRequest): Promise<void> {
    try {
      await httpClient.post(`/api/v1/users/${userId}/roles`, request);
    } catch (error) {
      throw new Error('Failed to change user roles');
    }
  }

  public async changeUserPassword(request: ChangeUserPasswordRequest): Promise<void> {
    try {
      await httpClient.post(`/api/v1/users/${request.userId}/change-password`, {
        newPassword: request.newPassword,
        requirePasswordChange: request.requirePasswordChange
      });
    } catch (error) {
      throw new Error('Failed to change user password');
    }
  }

  public async sendWelcomeEmail(userId: string): Promise<void> {
    try {
      await httpClient.post(`/api/v1/users/${userId}/welcome-email`);
    } catch (error) {
      throw new Error('Failed to send welcome email');
    }
  }

  public async sendPasswordResetEmail(userId: string): Promise<void> {
    try {
      await httpClient.post(`/api/v1/users/${userId}/password-reset-email`);
    } catch (error) {
      throw new Error('Failed to send password reset email');
    }
  }

  public async importUsers(request: UserImportRequest): Promise<UserImportResult> {
    try {
      const response = await httpClient.post<UserImportResult>('/api/v1/users/import', request);
      return response.data;
    } catch (error) {
      throw new Error('Failed to import users');
    }
  }

  public async importUsersFromCsv(
    file: File,
    sendWelcomeEmails: boolean = false,
    requirePasswordChange: boolean = true
  ): Promise<UserImportResult> {
    try {
      const formData = new FormData();
      formData.append('file', file);
      formData.append('sendWelcomeEmails', sendWelcomeEmails.toString());
      formData.append('requirePasswordChange', requirePasswordChange.toString());

      const response = await httpClient.upload<UserImportResult>('/api/v1/users/import-csv', formData);
      return response.data;
    } catch (error) {
      throw new Error('Failed to import users from CSV');
    }
  }

  public async bulkOperation(operation: BulkUserOperation): Promise<{
    successful: string[];
    failed: Array<{ userId: string; error: string }>;
  }> {
    try {
      const response = await httpClient.post(`/api/v1/users/bulk-operation`, operation);
      return response.data;
    } catch (error) {
      throw new Error('Failed to perform bulk operation');
    }
  }

  public async getUserActivity(
    userId: string,
    page: number = 1,
    pageSize: number = 50,
    startDate?: string,
    endDate?: string
  ): Promise<PaginatedResponse<UserActivity>> {
    try {
      const params = new URLSearchParams();
      params.append('page', page.toString());
      params.append('pageSize', Math.min(pageSize, 100).toString());
      
      if (startDate) params.append('startDate', startDate);
      if (endDate) params.append('endDate', endDate);

      const response = await httpClient.get<PaginatedResponse<UserActivity>>(`/api/v1/users/${userId}/activity?${params}`);
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch user activity');
    }
  }

  public async getUserStats(userId: string): Promise<UserStats> {
    try {
      const response = await httpClient.get<UserStats>(`/api/v1/users/${userId}/stats`);
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch user statistics');
    }
  }

  public async getUserDevices(userId: string): Promise<Array<{
    deviceId: string;
    deviceName?: string;
    lastSeen: string;
    isOnline: boolean;
    osVersion: string;
    registeredAt: string;
  }>> {
    try {
      const response = await httpClient.get(`/api/v1/users/${userId}/devices`);
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch user devices');
    }
  }

  public async getUserLicenses(userId: string): Promise<Array<{
    licenseId: string;
    tier: string;
    status: string;
    issuedAt: string;
    expiresAt?: string;
    maxDevices: number;
    assignedDevices: number;
  }>> {
    try {
      const response = await httpClient.get(`/api/v1/users/${userId}/licenses`);
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch user licenses');
    }
  }

  public async searchUsers(query: string, maxResults: number = 20): Promise<Array<{
    id: string;
    userName: string;
    email: string;
    roles: string[];
    department?: string;
    isActive: boolean;
  }>> {
    try {
      const params = new URLSearchParams();
      params.append('q', query);
      params.append('max', Math.min(maxResults, 50).toString());

      const response = await httpClient.get(`/api/v1/users/search?${params}`);
      return response.data;
    } catch (error) {
      throw new Error('Failed to search users');
    }
  }

  public async exportUsers(
    format: 'csv' | 'xlsx',
    filters?: UserFilters
  ): Promise<Blob> {
    try {
      const params = new URLSearchParams();
      params.append('format', format);
      
      if (filters?.search) params.append('search', filters.search);
      if (filters?.roles) filters.roles.forEach(role => params.append('roles', role));
      if (filters?.departments) filters.departments.forEach(dept => params.append('departments', dept));
      if (filters?.isActive !== undefined) params.append('isActive', filters.isActive.toString());

      const response = await httpClient.get(`/api/v1/users/export?${params}`, {
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
      throw new Error('Failed to export users');
    }
  }

  // Role management
  public async getAvailableRoles(): Promise<Array<{
    name: string;
    description: string;
    permissions: string[];
    userCount: number;
  }>> {
    try {
      const response = await httpClient.get('/api/v1/roles');
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch available roles');
    }
  }

  // Department management
  public async getDepartments(): Promise<Array<{
    name: string;
    userCount: number;
    managers: string[];
  }>> {
    try {
      const response = await httpClient.get('/api/v1/users/departments');
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch departments');
    }
  }

  // User session management
  public async getUserSessions(userId: string): Promise<Array<{
    sessionId: string;
    ipAddress: string;
    userAgent: string;
    lastActivity: string;
    location?: string;
    isCurrent: boolean;
  }>> {
    try {
      const response = await httpClient.get(`/api/v1/users/${userId}/sessions`);
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch user sessions');
    }
  }

  public async revokeUserSession(userId: string, sessionId: string): Promise<void> {
    try {
      await httpClient.delete(`/api/v1/users/${userId}/sessions/${sessionId}`);
    } catch (error) {
      throw new Error('Failed to revoke user session');
    }
  }

  public async revokeAllUserSessions(userId: string): Promise<void> {
    try {
      await httpClient.delete(`/api/v1/users/${userId}/sessions`);
    } catch (error) {
      throw new Error('Failed to revoke all user sessions');
    }
  }

  // Utility methods
  public getRoleDisplayName(role: string): string {
    const roleDisplayNames: Record<string, string> = {
      'Owner': 'Owner',
      'Admin': 'Administrator',
      'Manager': 'Manager',
      'Support': 'Support',
      'User': 'Standard User',
      'EnterpriseUser': 'Enterprise User',
      'ProUser': 'Pro User',
      'BasicUser': 'Basic User'
    };
    
    return roleDisplayNames[role] || role;
  }

  public getRoleLevel(role: string): number {
    const roleLevels: Record<string, number> = {
      'Owner': 100,
      'Admin': 90,
      'Manager': 70,
      'Support': 50,
      'EnterpriseUser': 30,
      'ProUser': 20,
      'BasicUser': 10,
      'User': 10
    };
    
    return roleLevels[role] || 0;
  }

  public isRoleHigherThan(role1: string, role2: string): boolean {
    return this.getRoleLevel(role1) > this.getRoleLevel(role2);
  }

  public calculateRiskScore(user: User, stats: UserStats): number {
    let risk = 0;
    
    // High privilege roles increase risk
    if (user.roles.includes('Owner') || user.roles.includes('Admin')) risk += 30;
    else if (user.roles.includes('Manager')) risk += 20;
    
    // Failed login attempts
    risk += Math.min(user.accessFailedCount * 5, 25);
    
    // No recent login activity
    const daysSinceLogin = stats.lastLogin ? 
      (Date.now() - new Date(stats.lastLogin).getTime()) / (1000 * 60 * 60 * 24) : 999;
    if (daysSinceLogin > 30) risk += 20;
    else if (daysSinceLogin > 7) risk += 10;
    
    // No 2FA on privileged account
    if (!user.twoFactorEnabled && (user.roles.includes('Admin') || user.roles.includes('Owner'))) {
      risk += 25;
    }
    
    return Math.min(risk, 100);
  }
}

// Global user service instance
export const userService = new UserService();
