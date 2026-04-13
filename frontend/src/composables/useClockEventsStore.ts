import { ref } from "vue";
import { clockEventService, type DaySummary } from "../services/clockEventService";

const summaries = ref<DaySummary[]>([]);
const loading = ref(false);

export function useClockEventsStore() {
  const fetchSummaries = async (force: boolean = false) => {
    if (summaries.value.length > 0 && !force) return summaries.value;
    try {
      loading.value = true;
      summaries.value = await clockEventService.getSummaries();
      return summaries.value;
    } catch (error) {
      console.error("Failed to load clock event summaries:", error);
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
