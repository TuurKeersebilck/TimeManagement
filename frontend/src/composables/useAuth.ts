import { ref, computed } from "vue";
import { authService, type User } from "../services/authService";
import { useRouter } from "vue-router";
import { useTimeLogsStore } from "./useTimeLogsStore";

const currentUser = ref<User | null>(null);
const isLoadingUser = ref(false);
const isUserFetched = ref(false);

export function useAuth() {
	const router = useRouter();

	const fetchUser = async (force = false) => {
		// Only fetch if not already fetched or if forced
		if (isUserFetched.value && !force) {
			return currentUser.value;
		}

		try {
			isLoadingUser.value = true;
			currentUser.value = await authService.getCurrentUser();
			isUserFetched.value = true;
			return currentUser.value;
		} catch (error) {
			console.error("Failed to fetch user:", error);
			// If token is invalid, redirect to login
			clearUser();
			authService.logout();
			router.push("/login");
			return null;
		} finally {
			isLoadingUser.value = false;
		}
	};

	const clearUser = () => {
		currentUser.value = null;
		isUserFetched.value = false;
		const { clearCache } = useTimeLogsStore();
		clearCache();
	};

	const userInitials = computed(() => {
		if (!currentUser.value) return "??";
		const names = currentUser.value.fullName.split(" ");
		if (names.length >= 2) {
			return (names[0][0] + names[names.length - 1][0]).toUpperCase();
		}
		return currentUser.value.fullName.substring(0, 2).toUpperCase();
	});

	return {
		currentUser,
		isLoadingUser,
		userInitials,
		fetchUser,
		clearUser,
	};
}
