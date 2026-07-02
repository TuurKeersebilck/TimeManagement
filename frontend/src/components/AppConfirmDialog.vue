<script setup lang="ts">
import { useConfirmDialog } from "@/composables/useConfirmDialog";
import {
  AlertDialog,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Button } from "@/components/ui/button";
import { Loader2Icon } from "lucide-vue-next";

const { isOpen, isConfirming, options, handleConfirm, handleCancel } = useConfirmDialog();
</script>

<template>
  <AlertDialog :open="isOpen" @update:open="(v) => !v && handleCancel()">
    <AlertDialogContent>
      <AlertDialogHeader>
        <AlertDialogTitle>{{ options?.title }}</AlertDialogTitle>
        <AlertDialogDescription>{{ options?.message }}</AlertDialogDescription>
      </AlertDialogHeader>
      <AlertDialogFooter>
        <Button variant="outline" :disabled="isConfirming" @click="handleCancel">
          {{ options?.cancelLabel ?? "Cancel" }}
        </Button>
        <Button
          :variant="options?.variant === 'destructive' ? 'destructive' : 'default'"
          :disabled="isConfirming"
          @click="handleConfirm"
        >
          <Loader2Icon v-if="isConfirming" class="size-3.5 animate-spin" />
          {{ options?.confirmLabel ?? "Confirm" }}
        </Button>
      </AlertDialogFooter>
    </AlertDialogContent>
  </AlertDialog>
</template>
