/**
 * Enterprise Analytics Service
 * Provides comprehensive analytics data for enterprise monitoring and insights
 */

import { httpClient } from './http';

export interface AnalyticsSummary {
  totalUsers: number;
  activeUsers: number;
  totalDevices: number;
  onlineDevices: number;
  totalTweaks: number;
  tweaksExecutedToday: number;
  tweaksExecutedThisWeek: number;
  tweaksExecutedThisMonth: number;
  successRate: number;
  averageExecutionTime: number;
  totalLicenses: number;
  activeLicenses: number;
  licenseUtilization: number;
  criticalIssues: number;
  warningIssues: number;
  lastUpdated: string;
}

export interface TopTweak {
  id: string;
  name: string;
  category: string;
  executionCount: number;
  successRate: number;
  averageExecutionTime: number;
  lastExecuted: string;
  riskLevel: 'Low' | 'Medium' | 'High' | 'Critical';
}

export interface DeviceAnalytics {
  deviceId: string;
  deviceName?: string;
  isOnline: boolean;
  lastSeen: string;
  osVersion: string;
  agentVersion: string;
  totalTweaks: number;
  successfulTweaks: number;
  failedTweaks: number;
  averageExecutionTime: number;
  lastTweakExecuted?: string;
  healthScore: number;
  issues: string[];
}

export interface UserAnalytics {
  userId: string;
  userName: string;
  email: string;
  roles: string[];
  lastLogin: string;
  totalTweaksExecuted: number;
  devicesManaged: number;
  licenseType: string;
  isActive: boolean;
  riskScore: number;
}

export interface LicenseByTier {
  tier: string;
  count: number;
  activeCount: number;
  utilizationRate: number;
  expiringWithin30Days: number;
  revenue?: number;
}

export interface TweakFailureAnalysis {
  tweakId: string;
  tweakName: string;
  category: string;
  failureCount: number;
  totalExecutions: number;
  failureRate: number;
  commonErrors: Array<{
    error: string;
    count: number;
    percentage: number;
  }>;
  affectedDevices: number;
  lastFailure: string;
  trend: 'increasing' | 'decreasing' | 'stable';
}

export interface SystemHealth {
  overallHealth: 'Excellent' | 'Good' | 'Warning' | 'Critical';
  serverUptime: string;
  databaseHealth: 'Healthy' | 'Warning' | 'Critical';
  hubConnectionsActive: number;
  hubConnectionsTotal: number;
  apiResponseTime: number;
  errorRate: number;
  memoryUsage: number;
  cpuUsage: number;
  diskUsage: number;
  activeSessionsCount: number;
  queuedOperations: number;
  lastHealthCheck: string;
}

export interface TimeSeriesDataPoint {
  timestamp: string;
  value: number;
  label?: string;
}

export interface AnalyticsFilters {
  startDate?: string;
  endDate?: string;
  deviceIds?: string[];
  userIds?: string[];
  tweakCategories?: string[];
  riskLevels?: string[];
  includeFailures?: boolean;
  includeSuccesses?: boolean;
}

export class AnalyticsService {
  public async getSummary(days: number = 30): Promise<AnalyticsSummary> {
    try {
      const response = await httpClient.get<AnalyticsSummary>(`/api/v1/analytics/summary?days=${days}`);
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch analytics summary');
    }
  }

  public async getTopTweaks(days: number = 30, top: number = 10): Promise<TopTweak[]> {
    try {
      const response = await httpClient.get<TopTweak[]>(`/api/v1/analytics/tweaks/top?days=${days}&top=${top}`);
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch top tweaks');
    }
  }

  public async getDeviceAnalytics(filters?: AnalyticsFilters): Promise<DeviceAnalytics[]> {
    try {
      const params = new URLSearchParams();
      if (filters?.startDate) params.append('startDate', filters.startDate);
      if (filters?.endDate) params.append('endDate', filters.endDate);
      if (filters?.deviceIds) filters.deviceIds.forEach(id => params.append('deviceIds', id));
      
      const response = await httpClient.get<DeviceAnalytics[]>(`/api/v1/analytics/devices?${params}`);
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch device analytics');
    }
  }

  public async getUserAnalytics(filters?: AnalyticsFilters): Promise<UserAnalytics[]> {
    try {
      const params = new URLSearchParams();
      if (filters?.startDate) params.append('startDate', filters.startDate);
      if (filters?.endDate) params.append('endDate', filters.endDate);
      if (filters?.userIds) filters.userIds.forEach(id => params.append('userIds', id));
      
      const response = await httpClient.get<UserAnalytics[]>(`/api/v1/analytics/users?${params}`);
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch user analytics');
    }
  }

  public async getLicensesByTier(): Promise<LicenseByTier[]> {
    try {
      const response = await httpClient.get<LicenseByTier[]>('/api/v1/analytics/licenses-by-tier');
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch license analytics by tier');
    }
  }

  public async getTweakFailures(days: number = 30, top: number = 20): Promise<TweakFailureAnalysis[]> {
    try {
      const response = await httpClient.get<TweakFailureAnalysis[]>(`/api/v1/analytics/tweaks-failures-top?days=${days}&top=${top}`);
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch tweak failure analysis');
    }
  }

  public async getActiveDevices(minutes: number = 30): Promise<string[]> {
    try {
      const response = await httpClient.get<string[]>(`/api/v1/analytics/active-devices?minutes=${minutes}`);
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch active devices');
    }
  }

  public async getSystemHealth(): Promise<SystemHealth> {
    try {
      const response = await httpClient.get<SystemHealth>('/api/v1/analytics/system-health');
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch system health');
    }
  }

  public async getExecutionTrends(days: number = 30): Promise<TimeSeriesDataPoint[]> {
    try {
      const response = await httpClient.get<TimeSeriesDataPoint[]>(`/api/v1/analytics/execution-trends?days=${days}`);
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch execution trends');
    }
  }

  public async getSuccessRateTrends(days: number = 30): Promise<TimeSeriesDataPoint[]> {
    try {
      const response = await httpClient.get<TimeSeriesDataPoint[]>(`/api/v1/analytics/success-rate-trends?days=${days}`);
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch success rate trends');
    }
  }

  public async getUserActivityTrends(days: number = 30): Promise<TimeSeriesDataPoint[]> {
    try {
      const response = await httpClient.get<TimeSeriesDataPoint[]>(`/api/v1/analytics/user-activity-trends?days=${days}`);
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch user activity trends');
    }
  }

  public async getDeviceOnlineStatus(): Promise<{ online: number; offline: number; total: number }> {
    try {
      const response = await httpClient.get<{ online: number; offline: number; total: number }>('/api/v1/analytics/device-status');
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch device online status');
    }
  }

  public async getPerformanceMetrics(hours: number = 24): Promise<{
    averageResponseTime: number;
    errorRate: number;
    throughput: number;
    concurrentUsers: number;
    systemLoad: TimeSeriesDataPoint[];
  }> {
    try {
      const response = await httpClient.get(`/api/v1/analytics/performance-metrics?hours=${hours}`);
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch performance metrics');
    }
  }

  public async getSecurityEvents(days: number = 7): Promise<Array<{
    timestamp: string;
    eventType: string;
    severity: 'Low' | 'Medium' | 'High' | 'Critical';
    description: string;
    userId?: string;
    deviceId?: string;
    ipAddress?: string;
  }>> {
    try {
      const response = await httpClient.get(`/api/v1/analytics/security-events?days=${days}`);
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch security events');
    }
  }

  public async getLicenseUtilization(): Promise<{
    totalLicenses: number;
    usedLicenses: number;
    availableLicenses: number;
    utilizationPercentage: number;
    expiringIn30Days: number;
    expiredLicenses: number;
    byTier: Array<{
      tier: string;
      total: number;
      used: number;
      available: number;
    }>;
  }> {
    try {
      const response = await httpClient.get('/api/v1/analytics/license-utilization');
      return response.data;
    } catch (error) {
      throw new Error('Failed to fetch license utilization');
    }
  }

  public async exportAnalyticsReport(
    type: 'summary' | 'detailed' | 'security' | 'performance',
    format: 'pdf' | 'xlsx' | 'csv',
    filters?: AnalyticsFilters
  ): Promise<Blob> {
    try {
      const params = new URLSearchParams();
      params.append('type', type);
      params.append('format', format);
      
      if (filters?.startDate) params.append('startDate', filters.startDate);
      if (filters?.endDate) params.append('endDate', filters.endDate);
      
      const response = await httpClient.get(`/api/v1/analytics/export?${params}`, {
        headers: { 'Accept': `application/${format}` }
      });
      
      // Convert response to blob for download
      return new Blob([response.data], { 
        type: format === 'pdf' ? 'application/pdf' : 
              format === 'xlsx' ? 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' :
              'text/csv'
      });
    } catch (error) {
      throw new Error('Failed to export analytics report');
    }
  }

  // Real-time analytics (if WebSocket support is available)
  public subscribeToRealTimeUpdates(
    callback: (update: { type: string; data: any }) => void
  ): () => void {
    // This would connect to a SignalR hub for real-time updates
    // Implementation depends on SignalR client setup
    console.log('Real-time analytics subscription would be implemented here');
    
    // Return unsubscribe function
    return () => {
      console.log('Unsubscribing from real-time analytics updates');
    };
  }

  // Utility methods
  public calculateHealthScore(
    successRate: number,
    averageResponseTime: number,
    errorCount: number
  ): number {
    const successWeight = 0.5;
    const performanceWeight = 0.3;
    const reliabilityWeight = 0.2;

    const successScore = Math.min(successRate, 100);
    const performanceScore = Math.max(0, 100 - (averageResponseTime / 1000) * 10);
    const reliabilityScore = Math.max(0, 100 - errorCount * 5);

    return Math.round(
      successScore * successWeight +
      performanceScore * performanceWeight +
      reliabilityScore * reliabilityWeight
    );
  }

  public formatUptime(uptimeSeconds: number): string {
    const days = Math.floor(uptimeSeconds / (24 * 3600));
    const hours = Math.floor((uptimeSeconds % (24 * 3600)) / 3600);
    const minutes = Math.floor((uptimeSeconds % 3600) / 60);

    if (days > 0) {
      return `${days}d ${hours}h ${minutes}m`;
    } else if (hours > 0) {
      return `${hours}h ${minutes}m`;
    } else {
      return `${minutes}m`;
    }
  }
}

// Global analytics service instance
export const analyticsService = new AnalyticsService();
