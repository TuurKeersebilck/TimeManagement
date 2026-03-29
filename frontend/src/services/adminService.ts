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

export const adminService = {
	async getAllTimeLogs(userId?: string): Promise<AdminTimeLog[]> {
		const params = userId ? { userId } : {};
		const response = await apiClient.get<AdminTimeLog[]>("/admin/timelogs", { params });
		return response.data;
	},

	async getEmployees(): Promise<Employee[]> {
		const response = await apiClient.get<Employee[]>("/admin/employees");
		return response.data;
	},
};
