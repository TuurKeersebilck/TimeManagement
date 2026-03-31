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

const { isOpen, options, handleConfirm, handleCancel } = useConfirmDialog();
</script>

<template>
  <AlertDialog :open="isOpen" @update:open="(v) => !v && handleCancel()">
    <AlertDialogContent>
      <AlertDialogHeader>
        <AlertDialogTitle>{{ options?.title }}</AlertDialogTitle>
        <AlertDialogDescription>{{ options?.message }}</AlertDialogDescription>
      </AlertDialogHeader>
      <AlertDialogFooter>
        <Button variant="outline" @click="handleCancel">
          {{ options?.cancelLabel ?? "Cancel" }}
        </Button>
        <Button
          :variant="options?.variant === 'destructive' ? 'destructive' : 'default'"
          @click="handleConfirm"
        >
          {{ options?.confirmLabel ?? "Confirm" }}
        </Button>
      </AlertDialogFooter>
    </AlertDialogContent>
  </AlertDialog>
</template>
