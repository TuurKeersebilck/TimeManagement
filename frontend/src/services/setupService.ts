import apiClient from "./api";

let _setupRequired: boolean | null = null;

export const setupService = {
  async isSetupRequired(): Promise<boolean> {
    if (_setupRequired !== null) return _setupRequired;
    const { data } = await apiClient.get("/setup/status");
    _setupRequired = data.setupRequired;
    return _setupRequired as boolean;
  },

  async complete(payload: {
    fullName: string;
    email: string;
    password: string;
    confirmPassword: string;
  }): Promise<void> {
    await apiClient.post("/setup/complete", payload);
    _setupRequired = false;
  },
};
