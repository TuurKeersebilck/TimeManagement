import { ref, computed } from "vue";
import { authService, type User } from "../services/authService";
import { useRouter } from "vue-router";
import { useTimeLogsStore } from "./useTimeLogsStore";

const currentUser = ref<User | null>(null);
const isLoadingUser = ref(false);
const isUserFetched = ref(false);
const storedRoles = ref<string[]>(authService.getRoles());

export function useAuth() {
  const router = useRouter();

  const fetchUser = async (force = false) => {
    if (isUserFetched.value && !force) {
      return currentUser.value;
    }

    try {
      isLoadingUser.value = true;
      currentUser.value = await authService.getCurrentUser();
      isUserFetched.value = true;
      storedRoles.value = currentUser.value.roles ?? [];
      return currentUser.value;
    } catch (error) {
      console.error("Failed to fetch user:", error);
      clearUser();
      authService.clearSession();
      router.push("/login");
      return null;
    } finally {
      isLoadingUser.value = false;
    }
  };

  const clearUser = () => {
    currentUser.value = null;
    isUserFetched.value = false;
    storedRoles.value = [];
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

  const isAdmin = computed(() => storedRoles.value.includes("Admin"));

  return {
    currentUser,
    isLoadingUser,
    isAdmin,
    userInitials,
    fetchUser,
    clearUser,
  };
}
