import { ref } from "vue";
import { vacationRequestService } from "@/services/vacationRequestService";

// Module-level so the sidebar badge and the Vacations page share one count.
const draftCount = ref(0);

/**
 * Vacation periods an employee planned but hasn't sent to the admin yet. Shown as a badge on
 * "My Vacations" so drafts aren't forgotten while browsing other pages.
 */
export function useDraftRequests() {
  const refreshDraftCount = async () => {
    try {
      const mine = await vacationRequestService.getMine();
      draftCount.value = mine.filter((r) => r.status === "Draft").length;
    } catch {
      // A badge is a hint — keep the last known count rather than surfacing an error.
    }
  };

  return { draftCount, refreshDraftCount };
}
