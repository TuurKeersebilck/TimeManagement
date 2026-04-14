import api from "./api";

export type AdjustmentRequestStatus = "Pending" | "Approved" | "Rejected";

export interface AdjustmentRequest {
  id: number;
  userId: string;
  employeeName: string;
  date: string; // "yyyy-MM-dd"
  requestedClockIn: string | null;
  requestedBreakStart: string | null;
  requestedBreakEnd: string | null;
  requestedClockOut: string | null;
  reason: string;
  status: AdjustmentRequestStatus;
  requestedAt: string;
  reviewedAt: string | null;
}

export interface CreateAdjustmentRequestPayload {
  date: string;
  requestedClockIn?: string;
  requestedBreakStart?: string;
  requestedBreakEnd?: string;
  requestedClockOut?: string;
  reason: string;
}

export const adjustmentRequestService = {
  create(payload: CreateAdjustmentRequestPayload): Promise<AdjustmentRequest> {
    return api.post("/timeadjustmentrequests", payload).then((r) => r.data);
  },

  getAll(): Promise<AdjustmentRequest[]> {
    return api.get("/timeadjustmentrequests").then((r) => r.data);
  },

  approve(id: number): Promise<void> {
    return api.post(`/timeadjustmentrequests/${id}/approve`).then(() => undefined);
  },

  reject(id: number): Promise<void> {
    return api.post(`/timeadjustmentrequests/${id}/reject`).then(() => undefined);
  },
};
