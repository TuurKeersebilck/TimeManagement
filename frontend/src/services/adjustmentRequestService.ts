import api from "./api";

export type AdjustmentRequestStatus = "Pending" | "Approved" | "Rejected";

export interface AdjustmentRequest {
  id: number;
  userId: string;
  employeeName: string;
  date: string; // "yyyy-MM-dd"
  desiredDaySnapshot: string; // JSON string
  reason: string;
  status: AdjustmentRequestStatus;
  requestedAt: string;
  reviewedAt: string | null;
  /** True when an admin edited the day directly; kept as an approved request for the history. */
  isAdminEdit: boolean;
}

export interface SnapshotBreak {
  breakRecordId?: number;
  breakStart: string; // ISO 8601 with offset
  breakEnd: string;
}

export interface SnapshotSession {
  workSessionId?: number;
  clockIn: string;
  clockOut: string;
  breaks: SnapshotBreak[];
}

export interface DesiredDaySnapshot {
  sessions: SnapshotSession[];
}

export interface AdminEditDayPayload {
  userId: string;
  date: string;
  /** The day's full intended state; an empty session list removes the day's hours. */
  desiredDaySnapshot: DesiredDaySnapshot;
  reason?: string;
}

export interface CreateAdjustmentRequestPayload {
  date: string;
  desiredDaySnapshot: DesiredDaySnapshot;
  reason: string;
}

export const adjustmentRequestService = {
  create(payload: CreateAdjustmentRequestPayload): Promise<AdjustmentRequest> {
    return api.post("/timeadjustmentrequests", payload).then((r) => r.data);
  },

  getMine(): Promise<AdjustmentRequest[]> {
    return api.get("/timeadjustmentrequests/mine").then((r) => r.data);
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

  /** Admin: replace an employee's sessions for one day directly, without a request. */
  adminEditDay(payload: AdminEditDayPayload): Promise<void> {
    return api.post("/timeadjustmentrequests/admin-edit", payload).then(() => undefined);
  },
};
