// Sign-out must finish the same save-before-leaving flow as normal navigation.
let leaveEditor: (() => Promise<boolean>) | undefined
export function registerEditorLeave(handler: () => Promise<boolean>) {
  leaveEditor = handler
  return () => {
    if (leaveEditor === handler) leaveEditor = undefined
  }
}
export async function prepareSignOut() {
  return leaveEditor ? leaveEditor() : true
}
