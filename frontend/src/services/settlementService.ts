import api from "./api";

export type SettlementOutcome = 0 | 1 | 2; // Paid=0 LeaveDeducted=1 Unpaid=2
export type SettlementStatus = 0 | 1; // PendingReview=0 Settled=1

export interface MonthlySettlementDto {
  id: number;
  userId: string;
  employeeName: string;
  year: number;
  month: number;
  netBalanceHours: number;
  overtimeHours: number;
  deficitHours: number;
  outcome: SettlementOutcome | null;
  status: SettlementStatus;
  reviewedByUserId: string | null;
  reviewedByName: string | null;
  reviewedAt: string | null;
  notes: string | null;
  generatedAt: string;
}

export interface BlockerDto {
  type: string;
  description: string;
}

export interface ConfirmSettlementPayload {
  outcome: SettlementOutcome;
  overtimeHoursOverride?: number;
  deficitHoursOverride?: number;
  notes?: string;
}

export const OUTCOME_LABELS: Record<SettlementOutcome, string> = {
  0: "Paid",
  1: "Leave Deducted",
  2: "Unpaid",
};

export const STATUS_LABELS: Record<SettlementStatus, string> = {
  0: "Pending Review",
  1: "Settled",
};

export const settlementService = {
  getAll(year: number, month: number): Promise<MonthlySettlementDto[]> {
    return api.get("/settlements", { params: { year, month } }).then((r) => r.data);
  },

  getById(id: number): Promise<MonthlySettlementDto> {
    return api.get(`/settlements/${id}`).then((r) => r.data);
  },

  confirm(id: number, payload: ConfirmSettlementPayload): Promise<void> {
    return api.post(`/settlements/${id}/confirm`, payload).then(() => undefined);
  },

  getEmployeeHistory(userId: string): Promise<MonthlySettlementDto[]> {
    return api.get(`/settlements/employee/${userId}`).then((r) => r.data);
  },
};
