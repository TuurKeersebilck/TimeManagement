import apiClient from "./api";

export interface VacationBalance {
  vacationTypeId: number;
  vacationTypeName: string;
  vacationTypeColor?: string;
  yearlyBalance: number;
  usedDays: number;
  remainingDays: number;
}

export interface VacationDay {
  id: number;
  vacationTypeId: number;
  vacationTypeName: string;
  vacationTypeColor?: string;
  date: string; // "YYYY-MM-DD"
  amount: number; // 0.5 or 1.0
  note?: string;
}

export interface CreateVacationDayDto {
  vacationTypeId: number;
  date: string;
  amount: number;
  note?: string;
}

export interface TeamVacationDay {
  id: number;
  userId: string;
  employeeName: string;
  date: string;
  amount: number;
  // Only populated for admins — the API strips these for regular employees
  vacationTypeId?: number;
  vacationTypeName?: string;
  vacationTypeColor?: string;
  note?: string;
}

export interface VacationType {
  id: number;
  name: string;
  color?: string;
}

export interface CreateVacationRangeDto {
  vacationTypeId: number;
  startDate: string;
  endDate: string;
  amount: number;
  note?: string;
}

export interface VacationRangeResult {
  created: VacationDay[];
  skippedWeekends: number;
  skippedHolidays: number;
  skippedExisting: number;
}

export const vacationService = {
  async getBalances(year?: number, employeeId?: string): Promise<VacationBalance[]> {
    const url = employeeId ? `/vacations/employees/${employeeId}/balances` : "/vacations/balances";
    const res = await apiClient.get<VacationBalance[]>(url, {
      params: year !== undefined ? { year } : undefined,
    });
    return res.data;
  },

  async getVacationDays(employeeId?: string): Promise<VacationDay[]> {
    const url = employeeId ? `/vacations/employees/${employeeId}` : "/vacations";
    const res = await apiClient.get<VacationDay[]>(url);
    return res.data;
  },

  async create(data: CreateVacationDayDto, employeeId?: string): Promise<VacationDay> {
    const url = employeeId ? `/vacations/employees/${employeeId}` : "/vacations";
    const res = await apiClient.post<VacationDay>(url, data);
    return res.data;
  },

  async update(id: number, data: CreateVacationDayDto, employeeId?: string): Promise<VacationDay> {
    const url = employeeId ? `/vacations/employees/${employeeId}/${id}` : `/vacations/${id}`;
    const res = await apiClient.put<VacationDay>(url, data);
    return res.data;
  },

  async delete(id: number, employeeId?: string): Promise<void> {
    const url = employeeId ? `/vacations/employees/${employeeId}/${id}` : `/vacations/${id}`;
    await apiClient.delete(url);
  },

  async createRange(data: CreateVacationRangeDto, employeeId?: string): Promise<VacationRangeResult> {
    const url = employeeId ? `/vacations/employees/${employeeId}/range` : "/vacations/range";
    const res = await apiClient.post<VacationRangeResult>(url, data);
    return res.data;
  },

  async getTeamVacationDays(params?: {
    year?: number;
    month?: number;
  }): Promise<TeamVacationDay[]> {
    const res = await apiClient.get<TeamVacationDay[]>("/vacations/team", { params });
    return res.data;
  },

  async getVacationTypes(): Promise<VacationType[]> {
    const res = await apiClient.get<VacationType[]>("/vacations/types");
    return res.data;
  },

  async getVacationForDate(date: string): Promise<VacationDay | null> {
    const res = await apiClient.get<VacationDay>(`/vacations/date/${date}`);
    return res.status === 204 ? null : res.data;
  },
};
