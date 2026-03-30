import apiClient from "./api";

export interface LoginCredentials {
	email: string;
	password: string;
	rememberMe: boolean;
}

export interface RegisterCredentials {
	fullName: string;
	email: string;
	password: string;
	confirmPassword: string;
}

export interface UpdateProfilePayload {
	fullName: string;
	email: string;
}

export interface ChangePasswordPayload {
	currentPassword: string;
	newPassword: string;
	confirmPassword: string;
}

export interface AuthResponse {
	token: string;
	email: string;
	fullName: string;
	roles: string[];
	message?: string;
}

export interface User {
	id: string;
	email: string;
	fullName: string;
	userName: string;
	roles: string[];
}

export const authService = {
	async login(credentials: LoginCredentials): Promise<AuthResponse> {
		const response = await apiClient.post<AuthResponse>(
			"/auth/login",
			credentials
		);
		return response.data;
	},

	async register(credentials: RegisterCredentials): Promise<AuthResponse> {
		const response = await apiClient.post<AuthResponse>(
			"/auth/register",
			credentials
		);
		return response.data;
	},

	async getCurrentUser(): Promise<User> {
		const response = await apiClient.get<User>("/auth/profile");
		return response.data;
	},

	async updateProfile(payload: UpdateProfilePayload): Promise<User> {
		const response = await apiClient.put<User>("/auth/profile", payload);
		return response.data;
	},

	async changePassword(payload: ChangePasswordPayload): Promise<void> {
		await apiClient.put("/auth/change-password", payload);
	},

	async logout(): Promise<void> {
		try {
			await apiClient.post("/auth/logout");
		} catch {
			// Ignore errors — session is cleared regardless
		}
		this.clearSession();
	},

	clearSession(): void {
		localStorage.removeItem("token");
		localStorage.removeItem("roles");
	},

	isAuthenticated(): boolean {
		return !!localStorage.getItem("token");
	},

	getToken(): string | null {
		return localStorage.getItem("token");
	},

	setToken(token: string): void {
		localStorage.setItem("token", token);
	},

	getRoles(): string[] {
		try {
			return JSON.parse(localStorage.getItem("roles") ?? "[]");
		} catch {
			return [];
		}
	},

	setRoles(roles: string[]): void {
		localStorage.setItem("roles", JSON.stringify(roles));
	},
};
