import { ref } from 'vue'

const collapsed = ref(localStorage.getItem('sidebar-collapsed') === '1')

export function useSidebar() {
  function toggle() {
    collapsed.value = !collapsed.value
    localStorage.setItem('sidebar-collapsed', collapsed.value ? '1' : '0')
  }

  return { collapsed, toggle }
}
