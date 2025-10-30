import axios from "axios";
import { authService } from "./authService";

const API_BASE_URL = "https://localhost:7055/api";

// Create axios instance with default config
const apiClient = axios.create({
	baseURL: API_BASE_URL,
	headers: {
		"Content-Type": "application/json",
	},
});

// Add token to requests if it exists
apiClient.interceptors.request.use((config) => {
	const token = authService.getToken();
	if (token) {
		config.headers.Authorization = `Bearer ${token}`;
	}
	return config;
});

export default apiClient;
