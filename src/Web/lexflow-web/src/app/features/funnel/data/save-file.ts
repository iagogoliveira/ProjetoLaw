/** Entrega ao navegador um arquivo baixado pela API (o DOCX da proposta, o modelo padrão). */
export function saveFile(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName.replace(/[\\/:*?"<>|]/g, '_');
  link.click();
  // Revogado depois: revogar no mesmo tique pode cancelar o download em alguns navegadores.
  setTimeout(() => URL.revokeObjectURL(url), 0);
}
