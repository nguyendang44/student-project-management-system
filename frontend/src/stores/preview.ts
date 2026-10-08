import { defineStore } from 'pinia'
import { ref } from 'vue'
export type Role = 'Student' | 'Lecturer' | 'Admin'
/** For UI shell inspection only; NOT an authenticated session. */
export const usePreviewStore = defineStore('preview', () => {
  const role = ref<Role>('Student')
  return { role }
})
