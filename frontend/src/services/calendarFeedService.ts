import apiClient from "./api";

export interface CalendarTokenInfoDto {
  hasToken: boolean;
  expiresAt: string | null;
}

export interface CalendarTokenDto {
  feedUrl: string;
  expiresAt: string;
}

export const calendarFeedService = {
  async getTokenInfo(): Promise<CalendarTokenInfoDto> {
    const { data } = await apiClient.get<CalendarTokenInfoDto>("/calendar/token");
    return data;
  },

  async regenerateToken(): Promise<CalendarTokenDto> {
    const { data } = await apiClient.post<CalendarTokenDto>("/calendar/token/regenerate");
    return data;
  },
};
