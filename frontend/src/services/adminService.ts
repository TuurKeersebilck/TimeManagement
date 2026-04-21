import apiClient from "./api";

export interface AdminTimeLog {
  userId: string;
  employeeName: string;
  employeeEmail: string;
  date: string;
  clockIn?: string;
  breakStart?: string;
  breakEnd?: string;
  clockOut?: string;
  description?: string;
  totalHours: number;
  isComplete: boolean;
  workedFromHome: boolean;
}

export interface Employee {
  id: string;
  fullName: string;
  email: string;
  weeklyHoursLogged: number;
  resolvedWeeklyTarget?: number | null;
}

export interface EmployeeTarget {
  dailyHours?: number | null;
  weeklyHours?: number | null;
  resolvedDailyHours?: number | null;
  resolvedWeeklyHours?: number | null;
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

export const adminService = {
  async getAllTimeLogs(params?: {
    userId?: string;
    dateFrom?: string;
    dateTo?: string;
  }): Promise<AdminTimeLog[]> {
    const response = await apiClient.get<AdminTimeLog[]>("/admin/timelogs", { params });
    return response.data;
  },

  async getEmployees(): Promise<Employee[]> {
    const response = await apiClient.get<Employee[]>("/admin/employees");
    return response.data;
  },

  async getEmployeeTarget(userId: string): Promise<EmployeeTarget> {
    const res = await apiClient.get<EmployeeTarget>(`/admin/employees/${userId}/target`);
    return res.data;
  },

  async setEmployeeTarget(userId: string, data: { dailyHours?: number | null; weeklyHours?: number | null; minimumBreakMinutes?: number | null }): Promise<EmployeeTarget> {
    const res = await apiClient.put<EmployeeTarget>(`/admin/employees/${userId}/target`, data);
    return res.data;
  },

  async getWeeklySummary(userId: string, weeks = 8): Promise<WeekSummary[]> {
    const res = await apiClient.get<WeekSummary[]>(`/admin/employees/${userId}/weekly-summary`, { params: { weeks } });
    return res.data;
  },

  async downloadPayrollExport(year: number, month: number, userId?: string): Promise<void> {
    const timezoneOffsetMinutes = -new Date().getTimezoneOffset();
    const response = await apiClient.get("/admin/export", {
      params: { year, month, userId: userId || undefined, timezoneOffsetMinutes },
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
};
