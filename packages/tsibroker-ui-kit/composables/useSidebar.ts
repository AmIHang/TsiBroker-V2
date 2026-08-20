import { ref } from 'vue'

// Collapsed by default - only an explicit "0" (the user expanded it before) opts back out.
const collapsed = ref(localStorage.getItem('sidebar-collapsed') !== '0')

export function useSidebar() {
  function toggle() {
    collapsed.value = !collapsed.value
    localStorage.setItem('sidebar-collapsed', collapsed.value ? '1' : '0')
  }

  return { collapsed, toggle }
}
