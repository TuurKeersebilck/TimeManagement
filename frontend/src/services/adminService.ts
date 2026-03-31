import apiClient from "./api";

export interface AdminTimeLog {
  id: number;
  userId: string;
  employeeName: string;
  employeeEmail: string;
  date: string;
  startTime: string;
  endTime: string;
  breakStart?: string;
  breakEnd?: string;
  description?: string;
  totalHours: number;
}

export interface Employee {
  id: string;
  fullName: string;
  email: string;
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
