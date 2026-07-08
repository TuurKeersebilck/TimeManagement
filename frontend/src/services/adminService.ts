import apiClient from "./api";
import type { WorkdayTargetDto } from "./holidayService";

export interface AdminBreak {
  breakStart: string;
  breakEnd?: string;
}

export interface AdminSession {
  clockIn: string;
  clockOut?: string;
  status: "Open" | "Closed" | "Invalidated";
  hours: number;
  breaks: AdminBreak[];
}

export interface AdminTimeLog {
  userId: string;
  employeeName: string;
  employeeEmail: string;
  date: string;
  totalHours: number;
  description?: string;
  workedFromHome: boolean;
  hasOpenSession: boolean;
  hasInvalidatedSession: boolean;
  sessions: AdminSession[];
}

export interface Employee {
  id: string;
  fullName: string;
  email: string;
  weeklyHoursLogged: number;
  resolvedWeeklyTarget?: number | null;
  isDisabled: boolean;
}

export interface EmployeeTarget {
  hasOverride: boolean;
  minimumBreakMinutes?: number | null;
  resolvedMinimumBreakMinutes?: number | null;
}

export interface WeekSummary {
  weekLabel: string;
  weekStart: string;
  hoursLogged: number;
  target?: number | null;
}

export interface MyTarget {
  dailyHours?: number | null;
  weeklyHours?: number | null;
}

export interface AdminVacationDay {
  id: number;
  userId: string;
  employeeName: string;
  vacationTypeId: number;
  vacationTypeName: string;
  vacationTypeColor?: string;
  date: string; // "YYYY-MM-DD"
  amount: number;
  note?: string;
}

export interface TimeBankAdjustment {
  id: number;
  userId: string;
  effectiveDate: string; // "YYYY-MM-DD"
  hours: number;
  reason: string;
  /** Set when this adjustment was auto-created by confirming a monthly settlement. */
  sourceSettlementId?: number | null;
  createdByUserId?: string | null;
  createdByName?: string | null;
  createdAt: string;
}

export interface CreateTimeBankAdjustmentInput {
  effectiveDate: string; // "YYYY-MM-DD"
  hours: number;
  reason: string;
}

export const adminService = {
  async getAllTimeLogs(params?: {
    userId?: string;
    dateFrom?: string;
    dateTo?: string;
  }): Promise<AdminTimeLog[]> {
    const response = await apiClient.get<AdminTimeLog[]>("/admin/timelogs", { params });
    return response.data;
  },

  async getEmployees(role?: "Employee" | "Admin"): Promise<Employee[]> {
    const response = await apiClient.get<Employee[]>("/admin/employees", {
      params: role ? { role } : undefined,
    });
    return response.data;
  },

  async disableEmployee(userId: string): Promise<void> {
    await apiClient.put(`/admin/employees/${userId}/disable`);
  },

  async enableEmployee(userId: string): Promise<void> {
    await apiClient.put(`/admin/employees/${userId}/enable`);
  },

  async deleteEmployee(userId: string): Promise<void> {
    await apiClient.delete(`/admin/employees/${userId}`);
  },

  async getEmployeeTarget(userId: string): Promise<EmployeeTarget> {
    const res = await apiClient.get<EmployeeTarget>(`/admin/employees/${userId}/target`);
    return res.data;
  },

  async setEmployeeTarget(userId: string, data: { minimumBreakMinutes?: number | null }): Promise<EmployeeTarget> {
    const res = await apiClient.put<EmployeeTarget>(`/admin/employees/${userId}/target`, data);
    return res.data;
  },

  async getEmployeeWorkdayTargets(userId: string): Promise<WorkdayTargetDto[]> {
    const res = await apiClient.get<WorkdayTargetDto[]>(`/admin/employees/${userId}/workday-targets`);
    return res.data;
  },

  async setEmployeeWorkdayTargets(userId: string, targets: WorkdayTargetDto[]): Promise<WorkdayTargetDto[]> {
    const res = await apiClient.put<WorkdayTargetDto[]>(`/admin/employees/${userId}/workday-targets`, { targets });
    return res.data;
  },

  async getWeeklySummary(userId: string, weeks = 8): Promise<WeekSummary[]> {
    const res = await apiClient.get<WeekSummary[]>(`/admin/employees/${userId}/weekly-summary`, { params: { weeks } });
    return res.data;
  },

  async downloadPayrollExport(year: number, month: number, userId?: string): Promise<void> {
    const response = await apiClient.get("/admin/export", {
      params: { year, month, userId: userId || undefined },
      responseType: "blob",
    });
    const url = URL.createObjectURL(new Blob([response.data], { type: "text/csv" }));
    const a = document.createElement("a");
    a.href = url;
    a.download = `payroll_${year}_${String(month).padStart(2, "0")}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  },

  async getAllVacationDays(filters?: {
    userId?: string;
    vacationTypeId?: number;
    year?: number;
    month?: number;
  }): Promise<AdminVacationDay[]> {
    const response = await apiClient.get<AdminVacationDay[]>("/admin/vacations", {
      params: filters,
    });
    return response.data;
  },

  async getTimeBankAdjustments(
    userId: string,
    filters?: { year?: number; month?: number }
  ): Promise<TimeBankAdjustment[]> {
    const response = await apiClient.get<TimeBankAdjustment[]>(
      `/admin/employees/${userId}/time-bank-adjustments`,
      { params: filters }
    );
    return response.data;
  },

  async createTimeBankAdjustment(
    userId: string,
    data: CreateTimeBankAdjustmentInput
  ): Promise<TimeBankAdjustment> {
    const response = await apiClient.post<TimeBankAdjustment>(
      `/admin/employees/${userId}/time-bank-adjustments`,
      data
    );
    return response.data;
  },

  async deleteTimeBankAdjustment(id: number): Promise<void> {
    await apiClient.delete(`/admin/time-bank-adjustments/${id}`);
  },
};
