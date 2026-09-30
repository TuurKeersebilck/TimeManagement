import { ref } from "vue";
import { adjustmentRequestService } from "@/services/adjustmentRequestService";

// Module-level so the sidebar badge and the requests page share one count.
const pendingCount = ref(0);

/**
 * Number of adjustment requests awaiting review. The requests page lives in a collapsed
 * submenu, so the sidebar shows this count to keep waiting employees from being overlooked.
 */
export function usePendingAdjustments() {
  const refreshPendingCount = async () => {
    try {
      const requests = await adjustmentRequestService.getAll();
      pendingCount.value = requests.filter((r) => r.status === "Pending").length;
    } catch {
      // A badge is a hint — keep the last known count rather than surfacing an error.
    }
  };

  return { pendingCount, refreshPendingCount };
}
