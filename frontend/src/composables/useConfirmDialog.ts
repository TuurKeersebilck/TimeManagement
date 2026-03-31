import { ref } from "vue";

export interface ConfirmOptions {
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  variant?: "default" | "destructive";
  onConfirm: () => void | Promise<void>;
  onCancel?: () => void;
}

const isOpen = ref(false);
const options = ref<ConfirmOptions | null>(null);

export function useConfirmDialog() {
  const confirm = (opts: ConfirmOptions) => {
    options.value = opts;
    isOpen.value = true;
  };

  const handleConfirm = async () => {
    await options.value?.onConfirm();
    isOpen.value = false;
    options.value = null;
  };

  const handleCancel = () => {
    options.value?.onCancel?.();
    isOpen.value = false;
    options.value = null;
  };

  return { confirm, isOpen, options, handleConfirm, handleCancel };
}
