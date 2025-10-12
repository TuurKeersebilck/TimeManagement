<template>
	<header class="bg-primary-600 text-white shadow-lg">
		<div class="container mx-auto px-6 py-4">
			<div class="flex justify-between items-center">
				<div class="flex items-center gap-3">
					<h1 class="text-2xl font-bold tracking-tight">Time Logger</h1>
				</div>
				<slot name="nav" />
			</div>
		</div>
	</header>
</template>
