/**
 * Enterprise Role Management Service
 * Handles role-based access control and permission management
 */

import { httpClient } from './http';

export interface Role {
  id: string;
  name: string;
  normalizedName: string;
  description: string;
  permissions: string[];
  userCount: number;
  isSystemRole: boolean;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  precedence: number; // Higher number = higher precedence
}

export interface Permission {
  id: string;
  name: string;
  category: string;
  description: string;
  isSystemPermission: boolean;
  dependsOn: string[]; // Other permissions this permission requires
}

export interface RolePermissionMatrix {
  roleId: string;
  roleName: string;
  permissions: Array<{
    permissionId: string;
    permissionName: string;
    category: string;
    granted: boolean;
    inherited: boolean; // If permission comes from a parent role
    source?: string; // Which role this permission is inherited from
  }>;
}

export interface CreateRoleRequest {
  name: string;
  description: string;
  permissions: string[];
  precedence?: number;
}

export interface UpdateRoleRequest {
  name?: string;
  description?: string;
  permissions?: string[];
  precedence?: number;
  isActive?: boolean;
}

export interface RoleAssignment {
  userId: string;
  userName: string;
  email: string;
  roles: Array<{
    roleId: string;
    roleName: string;
    assignedAt: string;
    assignedBy: string;
    expiresAt?: string;
  }>;
}

export interface BulkRoleOperation {
  userIds: string[];
  operation: 'add' | 'remove' | 'replace';
  roleIds: string[];
  reason?: string;
  expiresAt?: string;
}

export interface RoleHierarchy {
  roleId: string;
  roleName: string;
  level: number;
  children: RoleHierarchy[];
  permissions: string[];
  inheritedPermissions: string[];
}

export interface RoleUsageStats {
  roleId: string;
  roleName: string;
  activeUsers: number;
  totalUsers: number;
  recentAssignments: number; // In last 30 days
  averageSessionDuration: number;
  riskScore: number;
  commonPermissionsUsed: Array<{
    permission: string;
    usageCount: number;
    lastUsed: string;
  }>;
}

export class RoleService {
  public async getAllRoles(): Promise<Role[]> {
    try {
      const response = await httpClient.get<Role[]>('/api/v1/roles');
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch roles');
    }
  }

  public async getRoleById(roleId: string): Promise<Role> {
    try {
      const response = await httpClient.get<Role>(`/api/v1/roles/${roleId}`);
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch role');
    }
  }

  public async createRole(request: CreateRoleRequest): Promise<Role> {
    try {
      const response = await httpClient.post<Role>('/api/v1/roles', request);
      return response.data;
    } catch (error) {
      throw new Error('Failed to create role');
    }
  }

  public async updateRole(roleId: string, request: UpdateRoleRequest): Promise<Role> {
    try {
      const response = await httpClient.put<Role>(`/api/v1/roles/${roleId}`, request);
      return response.data;
    } catch (error) {
      throw new Error('Failed to update role');
    }
  }

  public async deleteRole(roleId: string, transferToRoleId?: string): Promise<void> {
    try {
      const body = transferToRoleId ? { transferToRoleId } : undefined;
      await httpClient.delete(`/api/v1/roles/${roleId}`, { 
        body: body ? JSON.stringify(body) : undefined 
      });
    } catch (error) {
      throw new Error('Failed to delete role');
    }
  }

  public async activateRole(roleId: string): Promise<void> {
    try {
      await httpClient.post(`/api/v1/roles/${roleId}/activate`);
    } catch (error) {
      throw new Error('Failed to activate role');
    }
  }

  public async deactivateRole(roleId: string): Promise<void> {
    try {
      await httpClient.post(`/api/v1/roles/${roleId}/deactivate`);
    } catch (error) {
      throw new Error('Failed to deactivate role');
    }
  }

  public async getAllPermissions(): Promise<Permission[]> {
    try {
      const response = await httpClient.get<Permission[]>('/api/v1/permissions');
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch permissions');
    }
  }

  public async getPermissionsByCategory(): Promise<Record<string, Permission[]>> {
    try {
      const permissions = await this.getAllPermissions();
      return permissions.reduce((acc, permission) => {
        if (!acc[permission.category]) {
          acc[permission.category] = [];
        }
        acc[permission.category].push(permission);
        return acc;
      }, {} as Record<string, Permission[]>);
    } catch (error) {
      throw new Error('Failed to fetch permissions by category');
    }
  }

  public async getRolePermissionMatrix(roleId: string): Promise<RolePermissionMatrix> {
    try {
      const response = await httpClient.get<RolePermissionMatrix>(`/api/v1/roles/${roleId}/permissions`);
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch role permission matrix');
    }
  }

  public async updateRolePermissions(roleId: string, permissionIds: string[]): Promise<void> {
    try {
      await httpClient.put(`/api/v1/roles/${roleId}/permissions`, { permissions: permissionIds });
    } catch (error) {
      throw new Error('Failed to update role permissions');
    }
  }

  public async addPermissionsToRole(roleId: string, permissionIds: string[]): Promise<void> {
    try {
      await httpClient.post(`/api/v1/roles/${roleId}/permissions`, { permissions: permissionIds });
    } catch (error) {
      throw new Error('Failed to add permissions to role');
    }
  }

  public async removePermissionsFromRole(roleId: string, permissionIds: string[]): Promise<void> {
    try {
      await httpClient.delete(`/api/v1/roles/${roleId}/permissions`, {
        body: JSON.stringify({ permissions: permissionIds })
      });
    } catch (error) {
      throw new Error('Failed to remove permissions from role');
    }
  }

  public async getUserRoles(userId: string): Promise<RoleAssignment> {
    try {
      const response = await httpClient.get<RoleAssignment>(`/api/v1/users/${userId}/roles`);
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch user roles');
    }
  }

  public async assignRolesToUser(
    userId: string, 
    roleIds: string[], 
    expiresAt?: string,
    reason?: string
  ): Promise<void> {
    try {
      await httpClient.post(`/api/v1/users/${userId}/roles`, {
        roleIds,
        expiresAt,
        reason
      });
    } catch (error) {
      throw new Error('Failed to assign roles to user');
    }
  }

  public async removeRolesFromUser(userId: string, roleIds: string[], reason?: string): Promise<void> {
    try {
      await httpClient.delete(`/api/v1/users/${userId}/roles`, {
        body: JSON.stringify({ roleIds, reason })
      });
    } catch (error) {
      throw new Error('Failed to remove roles from user');
    }
  }

  public async bulkRoleOperation(operation: BulkRoleOperation): Promise<{
    successful: Array<{ userId: string; message: string }>;
    failed: Array<{ userId: string; error: string }>;
    summary: {
      totalUsers: number;
      successful: number;
      failed: number;
    };
  }> {
    try {
      const response = await httpClient.post('/api/v1/roles/bulk-operation', operation);
      return response.data;
    } catch (error) {
      throw new Error('Failed to perform bulk role operation');
    }
  }

  public async getRoleHierarchy(): Promise<RoleHierarchy[]> {
    try {
      const response = await httpClient.get<RoleHierarchy[]>('/api/v1/roles/hierarchy');
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch role hierarchy');
    }
  }

  public async getRoleUsageStats(): Promise<RoleUsageStats[]> {
    try {
      const response = await httpClient.get<RoleUsageStats[]>('/api/v1/roles/usage-stats');
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch role usage statistics');
    }
  }

  public async getRoleUsers(
    roleId: string,
    page: number = 1,
    pageSize: number = 50
  ): Promise<{
    users: Array<{
      userId: string;
      userName: string;
      email: string;
      assignedAt: string;
      assignedBy: string;
      expiresAt?: string;
      isActive: boolean;
    }>;
    totalCount: number;
    pageNumber: number;
    pageSize: number;
    totalPages: number;
  }> {
    try {
      const params = new URLSearchParams();
      params.append('page', page.toString());
      params.append('pageSize', Math.min(pageSize, 100).toString());

      const response = await httpClient.get(`/api/v1/roles/${roleId}/users?${params}`);
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch role users');
    }
  }

  public async validateRolePermissions(roleId: string): Promise<{
    isValid: boolean;
    issues: Array<{
      type: 'missing_dependency' | 'conflicting_permission' | 'deprecated_permission';
      permission: string;
      description: string;
      recommendation: string;
    }>;
    recommendations: string[];
  }> {
    try {
      const response = await httpClient.get(`/api/v1/roles/${roleId}/validate`);
      return response.data;
    } catch (error) {
      throw new Error('Failed to validate role permissions');
    }
  }

  public async duplicateRole(roleId: string, newName: string, newDescription?: string): Promise<Role> {
    try {
      const response = await httpClient.post<Role>(`/api/v1/roles/${roleId}/duplicate`, {
        name: newName,
        description: newDescription
      });
      return response.data;
    } catch (error) {
      throw new Error('Failed to duplicate role');
    }
  }

  public async exportRoles(format: 'csv' | 'xlsx' | 'json'): Promise<Blob> {
    try {
      const response = await httpClient.get(`/api/v1/roles/export?format=${format}`, {
        headers: { 
          'Accept': format === 'xlsx' ? 
            'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' : 
            format === 'csv' ? 'text/csv' : 'application/json'
        }
      });
      
      return new Blob([response.data], { 
        type: format === 'xlsx' ? 
          'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' : 
          format === 'csv' ? 'text/csv' : 'application/json'
      });
    } catch (error) {
      throw new Error('Failed to export roles');
    }
  }

  public async importRoles(file: File): Promise<{
    successful: Array<{ roleName: string; message: string }>;
    failed: Array<{ roleName: string; error: string; rowNumber: number }>;
    summary: {
      total: number;
      successful: number;
      failed: number;
    };
  }> {
    try {
      const formData = new FormData();
      formData.append('file', file);

      const response = await httpClient.upload('/api/v1/roles/import', formData);
      return response.data;
    } catch (error) {
      throw new Error('Failed to import roles');
    }
  }

  public async auditRoleChanges(
    roleId?: string,
    startDate?: string,
    endDate?: string,
    page: number = 1,
    pageSize: number = 50
  ): Promise<{
    changes: Array<{
      id: string;
      roleId: string;
      roleName: string;
      action: 'created' | 'updated' | 'deleted' | 'permissions_changed' | 'user_assigned' | 'user_removed';
      details: Record<string, any>;
      performedBy: string;
      performedAt: string;
      ipAddress: string;
      reason?: string;
    }>;
    totalCount: number;
    pageNumber: number;
    pageSize: number;
  }> {
    try {
      const params = new URLSearchParams();
      params.append('page', page.toString());
      params.append('pageSize', Math.min(pageSize, 100).toString());
      
      if (roleId) params.append('roleId', roleId);
      if (startDate) params.append('startDate', startDate);
      if (endDate) params.append('endDate', endDate);

      const response = await httpClient.get(`/api/v1/roles/audit?${params}`);
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch role audit log');
    }
  }

  // Utility methods
  public getRoleDisplayName(roleName: string): string {
    const displayNames: Record<string, string> = {
      'Owner': 'System Owner',
      'Admin': 'Administrator',
      'Manager': 'Manager',
      'Support': 'Support Specialist',
      'User': 'Standard User',
      'EnterpriseUser': 'Enterprise User',
      'ProUser': 'Professional User',
      'BasicUser': 'Basic User'
    };
    
    return displayNames[roleName] || roleName;
  }

  public getRolePrecedence(roleName: string): number {
    const precedence: Record<string, number> = {
      'Owner': 100,
      'Admin': 90,
      'Manager': 70,
      'Support': 50,
      'EnterpriseUser': 30,
      'ProUser': 20,
      'BasicUser': 10,
      'User': 10
    };
    
    return precedence[roleName] || 0;
  }

  public getHighestRole(roles: string[]): string {
    return roles.reduce((highest, current) => {
      return this.getRolePrecedence(current) > this.getRolePrecedence(highest) ? current : highest;
    }, roles[0] || 'User');
  }

  public canRoleManageRole(managerRole: string, targetRole: string): boolean {
    return this.getRolePrecedence(managerRole) > this.getRolePrecedence(targetRole);
  }

  public getPermissionsByCategory(permissions: Permission[]): Record<string, Permission[]> {
    return permissions.reduce((acc, permission) => {
      if (!acc[permission.category]) {
        acc[permission.category] = [];
      }
      acc[permission.category].push(permission);
      return acc;
    }, {} as Record<string, Permission[]>);
  }

  public validatePermissionDependencies(
    selectedPermissions: string[], 
    allPermissions: Permission[]
  ): { isValid: boolean; missingDependencies: string[] } {
    const permissionMap = new Map(allPermissions.map(p => [p.id, p]));
    const missingDependencies: string[] = [];

    for (const permissionId of selectedPermissions) {
      const permission = permissionMap.get(permissionId);
      if (permission?.dependsOn) {
        for (const dependency of permission.dependsOn) {
          if (!selectedPermissions.includes(dependency)) {
            missingDependencies.push(dependency);
          }
        }
      }
    }

    return {
      isValid: missingDependencies.length === 0,
      missingDependencies: [...new Set(missingDependencies)]
    };
  }

  public calculateRoleRisk(role: Role, usageStats?: RoleUsageStats): number {
    let risk = 0;

    // High-privilege roles have inherent risk
    const privilegeRisk = Math.min(role.permissions.length * 2, 40);
    risk += privilegeRisk;

    // System roles have additional risk
    if (role.isSystemRole) risk += 20;

    // Usage patterns from stats
    if (usageStats) {
      // Many users with this role increases risk
      const userCountRisk = Math.min(usageStats.activeUsers * 0.5, 20);
      risk += userCountRisk;

      // Recent high assignment activity could indicate issues
      if (usageStats.recentAssignments > 10) risk += 10;
    }

    return Math.min(risk, 100);
  }
}

// Global role service instance
export const roleService = new RoleService();
