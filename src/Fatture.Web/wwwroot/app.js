const form = document.querySelector('#upload-form');
const input = document.querySelector('#file-input');
const dropZone = document.querySelector('#drop-zone');
const selectedFile = document.querySelector('#selected-file');
const fileName = document.querySelector('#file-name');
const fileSize = document.querySelector('#file-size');
const removeFile = document.querySelector('#remove-file');
const submitButton = document.querySelector('#submit-button');
const message = document.querySelector('#message');

function formatSize(bytes) {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

function showMessage(text, type) {
  message.textContent = text;
  message.className = `message ${type}`;
  message.hidden = false;
}

function updateFile(file) {
  message.hidden = true;
  const isZip = file && (file.name.toLowerCase().endsWith('.zip') || file.type === 'application/zip');
  if (!isZip) {
    input.value = '';
    selectedFile.hidden = true;
    submitButton.disabled = true;
    showMessage('Seleziona un archivio in formato ZIP.', 'error');
    return;
  }
  fileName.textContent = file.name;
  fileSize.textContent = formatSize(file.size);
  selectedFile.hidden = false;
  submitButton.disabled = false;
}

input.addEventListener('change', () => updateFile(input.files[0]));
['dragenter', 'dragover'].forEach(event => dropZone.addEventListener(event, e => { e.preventDefault(); dropZone.classList.add('dragging'); }));
['dragleave', 'drop'].forEach(event => dropZone.addEventListener(event, e => { e.preventDefault(); dropZone.classList.remove('dragging'); }));
dropZone.addEventListener('drop', event => {
  if (!event.dataTransfer.files.length) return;
  const transfer = new DataTransfer();
  transfer.items.add(event.dataTransfer.files[0]);
  input.files = transfer.files;
  updateFile(input.files[0]);
});
removeFile.addEventListener('click', () => {
  input.value = '';
  selectedFile.hidden = true;
  message.hidden = true;
  submitButton.disabled = true;
});

form.addEventListener('submit', async event => {
  event.preventDefault();
  if (!input.files.length) return;
  submitButton.disabled = true;
  submitButton.classList.add('loading');
  document.querySelector('.button-label').textContent = 'Elaborazione in corso…';
  message.hidden = true;
  try {
    const data = new FormData();
    data.append('file', input.files[0]);
    const response = await fetch('/api/fatture/report', { method: 'POST', body: data });
    if (!response.ok) throw new Error((await response.text()) || 'Impossibile elaborare il file.');
    const blob = await response.blob();
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'report-fatture.xlsx';
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
    showMessage('Report completato. Il download del file Excel è iniziato.', 'success');
  } catch (error) {
    showMessage(error.message || 'Si è verificato un errore inatteso.', 'error');
  } finally {
    submitButton.disabled = false;
    submitButton.classList.remove('loading');
    document.querySelector('.button-label').textContent = 'Elabora e scarica Excel';
  }
});
