<script setup>
import { ref, onMounted } from "vue";
import axios from "axios";

// PrimeVue componenten
import Button from "primevue/button";
import DataTable from "primevue/datatable";
import Column from "primevue/column";
import Dialog from "primevue/dialog";
import InputText from "primevue/inputtext";

const timeLogs = ref([]);
const showDialog = ref(false);
const form = ref({ date: "", startTime: "", endTime: "", break: "" });

onMounted(async () => {
	const res = await axios.get("https://localhost:5001/api/timelogs");
	timeLogs.value = res.data;
});

async function saveLog() {
	await axios.post("https://localhost:5001/api/timelogs", {
		date: form.value.date,
		startTime: form.value.startTime,
		endTime: form.value.endTime,
		break: form.value.break,
	});
	const res = await axios.get("https://localhost:5001/api/timelogs");
	timeLogs.value = res.data;
	showDialog.value = false;
}
</script>

<template>
	<div>
		<h2 class="text-2xl font-semibold mb-4">Urenregistratie</h2>

		<Button
			label="Nieuw Log"
			icon="pi pi-plus"
			class="mb-3"
			@click="showDialog = true"
		/>

		<DataTable
			:value="timeLogs"
			paginator
			rows="10"
			class="shadow-md rounded-xl"
		>
			<Column field="date" header="Datum" />
			<Column field="startTime" header="Starttijd" />
			<Column field="endTime" header="Eindtijd" />
			<Column field="break" header="Pauze" />
			<Column field="totalHours" header="Totaal (uren)" />
		</DataTable>

		<Dialog
			v-model:visible="showDialog"
			header="Nieuwe tijdsregistratie"
			:modal="true"
			class="w-96"
		>
			<div class="flex flex-col gap-3">
				<InputText v-model="form.date" placeholder="Datum (YYYY-MM-DD)" />
				<InputText v-model="form.startTime" placeholder="Starttijd (HH:mm)" />
				<InputText v-model="form.endTime" placeholder="Eindtijd (HH:mm)" />
				<InputText v-model="form.break" placeholder="Pauze (HH:mm)" />
				<Button label="Opslaan" icon="pi pi-check" @click="saveLog" />
			</div>
		</Dialog>
	</div>
</template>
