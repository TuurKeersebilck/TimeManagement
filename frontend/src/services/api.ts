import axios from "axios";
import { authService } from "./authService";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "https://localhost:7055/api";

// Create axios instance with default config
const apiClient = axios.create({
	baseURL: API_BASE_URL,
	headers: {
		"Content-Type": "application/json",
	},
});

// Attach JWT token to every request
apiClient.interceptors.request.use((config) => {
	const token = authService.getToken();
	if (token) {
		config.headers.Authorization = `Bearer ${token}`;
	}
	return config;
});

// On 401, clear session and redirect to login
apiClient.interceptors.response.use(
	(response) => response,
	(error) => {
		if (error.response?.status === 401) {
			const isLogoutCall = (error.config?.url as string | undefined)?.includes("/auth/logout");
			authService.clearSession();
			if (!isLogoutCall) {
				window.location.href = "/login";
			}
		}
		return Promise.reject(error);
	}
);

export default apiClient;
