import api from "./api";

export interface Invite {
  id: number;
  email: string;
  createdAt: string;
  expiresAt: string;
}

export interface ValidateInviteResponse {
  email: string;
}

export interface AcceptInvitePayload {
  token: string;
  fullName: string;
  password: string;
  confirmPassword: string;
}

export const inviteService = {
  validateToken(token: string): Promise<ValidateInviteResponse> {
    return api.get("/invites/validate", { params: { token } }).then((r) => r.data);
  },

  acceptInvite(payload: AcceptInvitePayload): Promise<unknown> {
    return api.post("/invites/accept", payload).then((r) => r.data);
  },

  createInvite(email: string): Promise<Invite> {
    return api.post("/invites", { email }).then((r) => r.data);
  },

  getPendingInvites(): Promise<Invite[]> {
    return api.get("/invites").then((r) => r.data);
  },

  cancelInvite(id: number): Promise<void> {
    return api.delete(`/invites/${id}`).then((r) => r.data);
  },
};
