import { toast } from "vue-sonner";

export function useAppToast() {
  return {
    success: (message: string, description?: string) => toast.success(message, { description }),

    error: (message: string, description?: string) =>
      toast.error(message, { description, duration: 5000 }),

    info: (message: string, description?: string) => toast.info(message, { description }),
  };
}
