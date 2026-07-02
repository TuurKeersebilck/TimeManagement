import { ref } from "vue";
import { workSessionService, type WorkDaySummaryDto } from "../services/workSessionService";

const summaries = ref<WorkDaySummaryDto[]>([]);
const loading = ref(false);

export function useClockEventsStore() {
  const fetchSummaries = async (force = false) => {
    if (summaries.value.length > 0 && !force) return summaries.value;
    try {
      loading.value = true;
      summaries.value = await workSessionService.getSummaries();
      return summaries.value;
    } catch (error) {
      console.error("Failed to load work session summaries:", error);
      throw error;
    } finally {
      loading.value = false;
    }
  };

  const refreshSummaries = async () => fetchSummaries(true);

  const clearCache = () => {
    summaries.value = [];
  };

  return { summaries, loading, fetchSummaries, refreshSummaries, clearCache };
}
