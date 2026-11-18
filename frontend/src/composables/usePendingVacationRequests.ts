import { ref } from "vue";
import { vacationRequestService } from "@/services/vacationRequestService";

// Module-level so the sidebar badge and the review page share one count.
const pendingVacationCount = ref(0);

/** Vacation requests employees submitted that still await an admin's review. */
export function usePendingVacationRequests() {
  const refreshPendingVacationCount = async () => {
    try {
      pendingVacationCount.value = await vacationRequestService.getPendingCount();
    } catch {
      // A badge is a hint — keep the last known count rather than surfacing an error.
    }
  };

  return { pendingVacationCount, refreshPendingVacationCount };
}
