import apiClient from "./api";

export interface PublicHoliday {
  id: number;
  date: string; // "YYYY-MM-DD"
  name: string;
  isCustom: boolean;
  /** True when the company works this day — it shows on the calendar but counts as a working day for vacation planning. */
  isWorkingDay: boolean;
}

export interface AppConfiguration {
  countryCode: string | null;
  countryName?: string | null;
  defaultDailyHours?: number | null;
  defaultWeeklyHours?: number | null;
  notificationEmail?: string | null;
  enableAdjustmentRequestEmails: boolean;
  enableMissedClockInEmails: boolean;
  minimumBreakMinutes?: number | null;
}

export interface AvailableCountry {
  countryCode: string;
  name: string;
}

export const holidayService = {
  // All authenticated users
  async getHolidays(year: number): Promise<PublicHoliday[]> {
    const res = await apiClient.get<PublicHoliday[]>(`/publicholidays/${year}`);
    return res.data;
  },

  // Admin only
  async getConfiguration(): Promise<AppConfiguration> {
    const res = await apiClient.get<AppConfiguration>("/admin/settings");
    return res.data;
  },

  async getAvailableCountries(): Promise<AvailableCountry[]> {
    const res = await apiClient.get<AvailableCountry[]>("/admin/settings/available-countries");
    return res.data;
  },

  async setCountry(countryCode: string): Promise<AppConfiguration> {
    const res = await apiClient.put<AppConfiguration>("/admin/settings/country", { countryCode });
    return res.data;
  },

  async getAdminHolidays(year: number): Promise<PublicHoliday[]> {
    const res = await apiClient.get<PublicHoliday[]>(`/admin/settings/holidays/${year}`);
    return res.data;
  },

  async refreshHolidays(year: number): Promise<PublicHoliday[]> {
    const res = await apiClient.post<PublicHoliday[]>(`/admin/settings/holidays/refresh/${year}`);
    return res.data;
  },

  async addCustomHoliday(date: string, name: string): Promise<PublicHoliday> {
    const res = await apiClient.post<PublicHoliday>("/admin/settings/holidays", { date, name });
    return res.data;
  },

  async setIsWorkingDay(id: number, isWorkingDay: boolean): Promise<PublicHoliday> {
    const res = await apiClient.patch<PublicHoliday>(`/admin/settings/holidays/${id}/is-working-day`, { isWorkingDay });
    return res.data;
  },

  async deleteHoliday(id: number): Promise<void> {
    await apiClient.delete(`/admin/settings/holidays/${id}`);
  },

  async setDefaultTargets(defaultDailyHours: number | null, defaultWeeklyHours: number | null): Promise<AppConfiguration> {
    const res = await apiClient.put<AppConfiguration>("/admin/settings/targets", { defaultDailyHours, defaultWeeklyHours });
    return res.data;
  },

  async setNotificationEmail(email: string | null): Promise<AppConfiguration> {
    const res = await apiClient.put<AppConfiguration>("/admin/settings/notification-email", { email });
    return res.data;
  },

  async setNotificationToggles(enableAdjustmentRequestEmails: boolean, enableMissedClockInEmails: boolean): Promise<AppConfiguration> {
    const res = await apiClient.put<AppConfiguration>("/admin/settings/notification-toggles", {
      enableAdjustmentRequestEmails,
      enableMissedClockInEmails,
    });
    return res.data;
  },

  async setMinimumBreakMinutes(minimumBreakMinutes: number | null): Promise<AppConfiguration> {
    const res = await apiClient.put<AppConfiguration>("/admin/settings/min-break", { minimumBreakMinutes });
    return res.data;
  },
};
