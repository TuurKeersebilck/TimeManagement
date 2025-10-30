import apiClient from "./api";

export interface LoginCredentials {
	email: string;
	password: string;
	rememberMe: boolean;
}

export interface RegisterCredentials {
	fullname: string;
	email: string;
	password: string;
	confirmPassword: string;
}

export interface AuthResponse {
	token: string;
	message?: string;
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

	logout(): void {
		localStorage.removeItem("token");
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
};
