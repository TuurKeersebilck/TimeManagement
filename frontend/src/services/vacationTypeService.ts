import apiClient from "./api";

export interface VacationType {
  id: number;
  name: string;
  description?: string;
  color?: string;
  isUnlimited: boolean;
  isAdminOnly: boolean;
  assignedEmployeeCount: number;
}

export interface VacationTypeForm {
  name: string;
  description?: string;
  color?: string;
  isUnlimited: boolean;
  isAdminOnly: boolean;
}

export interface EmployeeVacationBalance {
  id: number;
  vacationTypeId: number;
  vacationTypeName: string;
  vacationTypeColor?: string;
  yearlyBalance: number;
}

export const vacationTypeService = {
  async getAll(): Promise<VacationType[]> {
    const res = await apiClient.get<VacationType[]>("/admin/vacation-types");
    return res.data;
  },

  async create(data: VacationTypeForm): Promise<VacationType> {
    const res = await apiClient.post<VacationType>("/admin/vacation-types", data);
    return res.data;
  },

  async update(id: number, data: VacationTypeForm): Promise<VacationType> {
    const res = await apiClient.put<VacationType>(`/admin/vacation-types/${id}`, data);
    return res.data;
  },

  async delete(id: number): Promise<void> {
    await apiClient.delete(`/admin/vacation-types/${id}`);
  },

  async getEmployeeBalances(userId: string): Promise<EmployeeVacationBalance[]> {
    const res = await apiClient.get<EmployeeVacationBalance[]>(
      `/admin/employees/${userId}/vacation-balances`
    );
    return res.data;
  },

  async assignType(
    userId: string,
    data: { vacationTypeId: number; yearlyBalance: number }
  ): Promise<EmployeeVacationBalance> {
    const res = await apiClient.post<EmployeeVacationBalance>(
      `/admin/employees/${userId}/vacation-balances`,
      data
    );
    return res.data;
  },

  async updateBalance(
    userId: string,
    balanceId: number,
    yearlyBalance: number
  ): Promise<EmployeeVacationBalance> {
    const res = await apiClient.put<EmployeeVacationBalance>(
      `/admin/employees/${userId}/vacation-balances/${balanceId}`,
      { yearlyBalance }
    );
    return res.data;
  },

  async removeBalance(userId: string, balanceId: number): Promise<void> {
    await apiClient.delete(`/admin/employees/${userId}/vacation-balances/${balanceId}`);
  },
};
