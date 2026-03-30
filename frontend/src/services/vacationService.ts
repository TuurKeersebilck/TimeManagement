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

export const vacationService = {
	async getBalances(): Promise<VacationBalance[]> {
		const res = await apiClient.get<VacationBalance[]>("/vacations/balances");
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
};
