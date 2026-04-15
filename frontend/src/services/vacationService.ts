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
  vacationTypeId: number;
  vacationTypeName: string;
  vacationTypeColor?: string;
  date: string;
  amount: number;
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
  async getBalances(year?: number): Promise<VacationBalance[]> {
    const res = await apiClient.get<VacationBalance[]>("/vacations/balances", {
      params: year !== undefined ? { year } : undefined,
    });
    return res.data;
  },

  async getVacationDays(): Promise<VacationDay[]> {
    const res = await apiClient.get<VacationDay[]>("/vacations");
    return res.data;
  },

  async create(data: CreateVacationDayDto): Promise<VacationDay> {
    const res = await apiClient.post<VacationDay>("/vacations", data);
    return res.data;
  },

  async update(id: number, data: CreateVacationDayDto): Promise<VacationDay> {
    const res = await apiClient.put<VacationDay>(`/vacations/${id}`, data);
    return res.data;
  },

  async delete(id: number): Promise<void> {
    await apiClient.delete(`/vacations/${id}`);
  },

  async createRange(data: CreateVacationRangeDto): Promise<VacationRangeResult> {
    const res = await apiClient.post<VacationRangeResult>("/vacations/range", data);
    return res.data;
  },

  async getTeamVacationDays(params?: {
    vacationTypeId?: number;
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
