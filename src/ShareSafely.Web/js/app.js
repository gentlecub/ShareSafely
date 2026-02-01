// Configuración
// En Docker, usa '/api' porque nginx hace proxy
// En desarrollo local, usa 'https://localhost:7001/api'
const API_URL = window.location.hostname === 'localhost' && window.location.port === ''
    ? '/api'
    : (window.location.protocol + '//' + window.location.host + '/api');

// Elementos del DOM
const dropZone = document.getElementById('dropZone');
const fileInput = document.getElementById('fileInput');
const uploadBtn = document.getElementById('uploadBtn');
const expiration = document.getElementById('expiration');
const progressContainer = document.getElementById('progressContainer');
const progressFill = document.getElementById('progressFill');
const progressText = document.getElementById('progressText');
const resultSection = document.getElementById('resultSection');
const linkOutput = document.getElementById('linkOutput');
const copyBtn = document.getElementById('copyBtn');
const expiryDate = document.getElementById('expiryDate');
const newUploadBtn = document.getElementById('newUploadBtn');
const errorMessage = document.getElementById('errorMessage');

let selectedFile = null;

// Drag & Drop
dropZone.addEventListener('dragover', (e) => {
    e.preventDefault();
    dropZone.classList.add('dragover');
});

dropZone.addEventListener('dragleave', () => {
    dropZone.classList.remove('dragover');
});

dropZone.addEventListener('drop', (e) => {
    e.preventDefault();
    dropZone.classList.remove('dragover');
    const files = e.dataTransfer.files;
    if (files.length > 0) {
        handleFileSelect(files[0]);
    }
});

dropZone.addEventListener('click', (e) => {
    // Evitar que se abra el diálogo dos veces si el clic viene del label o del input
    // El label ya activa el input automáticamente por comportamiento nativo
    const clickedLabel = e.target.closest('label');
    if (e.target === fileInput || clickedLabel) {
        return;
    }
    fileInput.click();
});

fileInput.addEventListener('change', (e) => {
    if (e.target.files.length > 0) {
        handleFileSelect(e.target.files[0]);
    }
});

// Manejar selección de archivo
function handleFileSelect(file) {
    const allowedTypes = ['.pdf', '.docx', '.xlsx', '.png', '.jpg', '.jpeg', '.zip'];
    const maxSize = 100 * 1024 * 1024; // 100MB

    const ext = '.' + file.name.split('.').pop().toLowerCase();

    if (!allowedTypes.includes(ext)) {
        showError(`Unallowed file type: ${ext}`);
        return;
    }

    if (file.size > maxSize) {
        showError('The file exceeds the 100MB limit');
        return;
    }

    selectedFile = file;
    uploadBtn.disabled = false;
    hideError();

    // Actualizar UI
    dropZone.querySelector('p').textContent = file.name;
    dropZone.querySelector('.icon').textContent = '✅';
}

// Subir archivo
uploadBtn.addEventListener('click', async () => {
    if (!selectedFile) return;

    uploadBtn.disabled = true;
    progressContainer.classList.remove('hidden');
    hideError();

    try {
        // 1. Subir archivo
        const fileResponse = await uploadFile(selectedFile);

        // 2. Generar enlace
        const linkResponse = await generateLink(
            fileResponse.data.id,
            parseInt(expiration.value)
        );

        // 3. Mostrar resultado
        showResult(linkResponse.data);

    } catch (error) {
        showError(error.message || 'Error uploading file');
        resetUpload();
    }
});

// Llamada API: Subir archivo
async function uploadFile(file) {
    const formData = new FormData();
    formData.append('archivo', file);
    formData.append('expiracionMinutos', expiration.value);

    const xhr = new XMLHttpRequest();

    return new Promise((resolve, reject) => {
        xhr.upload.addEventListener('progress', (e) => {
            if (e.lengthComputable) {
                const percent = Math.round((e.loaded / e.total) * 100);
                progressFill.style.width = percent + '%';
                progressText.textContent = percent + '%';
            }
        });

        xhr.addEventListener('load', () => {
            if (xhr.status >= 200 && xhr.status < 300) {
                resolve(JSON.parse(xhr.responseText));
            } else {
                const error = JSON.parse(xhr.responseText);
                reject(new Error(error.message || 'Upload error'));
            }
        });

        xhr.addEventListener('error', () => {
            reject(new Error('Connection error'));
        });

        xhr.open('POST', `${API_URL}/files/upload`);
        xhr.send(formData);
    });
}

// Llamada API: Generar enlace
async function generateLink(archivoId, expiracionMinutos) {
    const response = await fetch(`${API_URL}/links/generate`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({ archivoId, expiracionMinutos })
    });

    if (!response.ok) {
        const error = await response.json();
        throw new Error(error.message || 'Error generating link');
    }

    return response.json();
}

// Mostrar resultado
function showResult(linkData) {
    progressContainer.classList.add('hidden');
    resultSection.classList.remove('hidden');

    linkOutput.value = linkData.url;
    expiryDate.textContent = new Date(linkData.fechaExpiracion).toLocaleString('es-ES');
}

// Copiar enlace
copyBtn.addEventListener('click', async () => {
    try {
        await navigator.clipboard.writeText(linkOutput.value);
        copyBtn.textContent = 'Copiado!';
        setTimeout(() => {
            copyBtn.textContent = 'Copiar';
        }, 2000);
    } catch {
        linkOutput.select();
        document.execCommand('copy');
    }
});

// Nueva subida
newUploadBtn.addEventListener('click', () => {
    resetUpload();
    resultSection.classList.add('hidden');
});

// Reset
function resetUpload() {
    selectedFile = null;
    fileInput.value = '';
    uploadBtn.disabled = true;
    progressContainer.classList.add('hidden');
    progressFill.style.width = '0%';
    progressText.textContent = '0%';
    dropZone.querySelector('p').textContent = 'Drag your file here';
    dropZone.querySelector('.icon').textContent = '📁';
}

// Errores
function showError(message) {
    errorMessage.textContent = message;
    errorMessage.classList.remove('hidden');
}

function hideError() {
    errorMessage.classList.add('hidden');
}
