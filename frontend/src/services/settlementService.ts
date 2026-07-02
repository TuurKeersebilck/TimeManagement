import api from "./api";

export type SettlementOutcome = "Paid" | "LeaveDeducted" | "Unpaid";
export type SettlementStatus = "PendingReview" | "Settled";

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
  Paid: "Paid",
  LeaveDeducted: "Leave Deducted",
  Unpaid: "Unpaid",
};

export const STATUS_LABELS: Record<SettlementStatus, string> = {
  PendingReview: "Pending Review",
  Settled: "Settled",
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

  generate(year: number, month: number): Promise<MonthlySettlementDto[]> {
    return api.post("/settlements/generate", null, { params: { year, month } }).then((r) => r.data);
  },
};
