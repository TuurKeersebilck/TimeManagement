import apiClient from "./api";
import type { VacationRangeResult } from "./vacationService";

/** "Draft": planned by the employee but not sent yet; only they see it. */
export type VacationRequestStatus = "Draft" | "Pending" | "Approved" | "Rejected" | "Cancelled";

export interface VacationRequestBalance {
  /** Null for an unlimited type, or one no longer assigned to the employee. */
  yearlyBalance: number | null;
  usedDays: number;
  /** Days held by the employee's other pending requests of this type. */
  otherPendingDays: number;
  /** Left over if this (and every other pending) request is approved. Null when unlimited. */
  remainingAfterApproval: number | null;
}

export interface VacationRequestOverlap {
  employeeName: string;
  dates: string[]; // "YYYY-MM-DD"
  /** True when the colleague's days are a request still awaiting review. */
  isPending: boolean;
}

export interface VacationRequest {
  id: number;
  userId: string;
  employeeName: string;
  vacationTypeId: number;
  vacationTypeName: string;
  vacationTypeColor?: string;
  startDate: string; // "YYYY-MM-DD"
  endDate: string;
  amount: number; // per day: 0.5 or 1
  totalDays: number;
  note?: string;
  status: VacationRequestStatus;
  requestedAt: string;
  reviewedAt: string | null;
  reviewedByName: string | null;
  /** The admin's explanation when a request is denied. */
  reviewNote: string | null;
  /** Admin list, pending requests only. */
  balance?: VacationRequestBalance | null;
  overlaps?: VacationRequestOverlap[] | null;
}

export interface CreateVacationRequestPayload {
  vacationTypeId: number;
  startDate: string;
  endDate: string;
  amount: number;
  note?: string;
}

export const vacationRequestService = {
  /** Adds a draft; nobody is told until submit(). */
  async create(payload: CreateVacationRequestPayload): Promise<VacationRequest> {
    const res = await apiClient.post<VacationRequest>("/vacationrequests", payload);
    return res.data;
  },

  async getMine(): Promise<VacationRequest[]> {
    const res = await apiClient.get<VacationRequest[]>("/vacationrequests/mine");
    return res.data;
  },

  async deleteDraft(id: number): Promise<void> {
    await apiClient.delete(`/vacationrequests/${id}`);
  },

  /** Sends all of the caller's drafts to the admin at once; returns them, now pending. */
  async submit(): Promise<VacationRequest[]> {
    const res = await apiClient.post<VacationRequest[]>("/vacationrequests/submit");
    return res.data;
  },

  /** Withdraws a submitted request that hasn't been reviewed yet. */
  async cancel(id: number): Promise<void> {
    await apiClient.post(`/vacationrequests/${id}/cancel`);
  },

  async getAll(): Promise<VacationRequest[]> {
    const res = await apiClient.get<VacationRequest[]>("/vacationrequests");
    return res.data;
  },

  async getPendingCount(): Promise<number> {
    const res = await apiClient.get<{ count: number }>("/vacationrequests/pending-count");
    return res.data.count;
  },

  async approve(id: number): Promise<VacationRangeResult> {
    const res = await apiClient.post<VacationRangeResult>(`/vacationrequests/${id}/approve`);
    return res.data;
  },

  async reject(id: number, reason?: string): Promise<void> {
    await apiClient.post(`/vacationrequests/${id}/reject`, { reason: reason?.trim() || undefined });
  },
};
