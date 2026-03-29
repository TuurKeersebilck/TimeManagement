import apiClient from "./api";

export interface TimeLog {
	id: number;
	date: string;
	startTime: string;
	endTime: string;
	breakStart?: string;
	breakEnd?: string;
	description?: string;
	totalHours: number;
}

export interface TimeLogCreate {
	date: string;
	startTime: string;
	endTime: string;
	breakStart?: string;
	breakEnd?: string;
	description?: string;
}

export interface TimeLogUpdate {
	date: string;
	startTime: string;
	endTime: string;
	breakStart?: string;
	breakEnd?: string;
	description?: string;
}

export const timeLogService = {
	async getAll(): Promise<TimeLog[]> {
		const response = await apiClient.get<TimeLog[]>("/timelogs");
		return response.data;
	},

	async getById(id: number): Promise<TimeLog> {
		const response = await apiClient.get<TimeLog>(`/timelogs/${id}`);
		return response.data;
	},

	async create(data: TimeLogCreate): Promise<TimeLog> {
		const response = await apiClient.post<TimeLog>("/timelogs", data);
		return response.data;
	},

	async update(id: number, data: TimeLogUpdate): Promise<void> {
		await apiClient.put(`/timelogs/${id}`, data);
	},

	async delete(id: number): Promise<void> {
		await apiClient.delete(`/timelogs/${id}`);
	},
};
