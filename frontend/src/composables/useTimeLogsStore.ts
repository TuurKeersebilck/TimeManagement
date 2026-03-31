import { ref } from "vue";
import { timeLogService, type TimeLog } from "../services/timeLogService";

const timeLogs = ref<TimeLog[]>([]);
const loading = ref(false);

export function useTimeLogsStore() {
  // Only fetch if not already fetched or if forced
  const fetchTimeLogs = async (force: boolean = false) => {
    if (timeLogs.value.length > 0 && !force) {
      return timeLogs.value;
    }
    try {
      loading.value = true;
      timeLogs.value = await timeLogService.getAll();
      return timeLogs.value;
    } catch (error) {
      console.error("Failed to load time logs:", error);
      throw error;
    } finally {
      loading.value = false;
    }
  };

  const refreshTimeLogs = async () => {
    await fetchTimeLogs(true);
  };

  const clearCache = () => {
    timeLogs.value = [];
  };

  return {
    timeLogs,
    loading,
    fetchTimeLogs,
    refreshTimeLogs,
    clearCache,
  };
}
