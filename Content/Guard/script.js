// script.js - Enhanced Guard Check-In System with Shift Validation

// Store today's data in session
let checkinData = JSON.parse(sessionStorage.getItem("checkinData")) || [];
const today = new Date().toISOString().split("T")[0];
let isValidGuard = false;
let currentGuardData = null;
let currentShiftType = null;
let currentShiftData = null;

// Configuration
const DAY_SHIFT_CHECKIN = "06:00";
const DAY_SHIFT_CHECKOUT = "18:00";
const NIGHT_SHIFT_CHECKIN = "18:00";
const NIGHT_SHIFT_CHECKOUT = "06:00";

// Utility functions
function generateShortCode() {
    return Math.random().toString(36).substring(2, 8).toUpperCase();
}

function getCurrentTimeString() {
    const now = new Date();
    return now.toTimeString().split(' ')[0].substring(0, 5);
}

function determineShiftType() {
    const now = new Date();
    const hours = now.getHours();
    return (hours >= 6 && hours < 18) ? 'Day' : 'Night';
}

function getExpectedTime(shiftType, action) {
    if (shiftType === 'Day') {
        return action === 'checkin' ? DAY_SHIFT_CHECKIN : DAY_SHIFT_CHECKOUT;
    } else {
        return action === 'checkin' ? NIGHT_SHIFT_CHECKIN : NIGHT_SHIFT_CHECKOUT;
    }
}

function showMessage(text, type) {
    const element = document.getElementById("statusMessage");
    element.textContent = text;
    element.className = `message ${type}`;
    element.style.display = "block";
}

function showValidationMessage(text, type) {
    const element = document.getElementById("validationMessage");
    element.textContent = text;
    element.className = `message ${type}`;
    element.style.display = "block";
}

// Navigation function
function goToDashboard() {
    window.location.href = '/Guard/Dashboard';
}

// API CALLS
async function validateGuardByUsername(siteUsername) {
    try {
        const response = await fetch('/Guard/ValidateGuardByUsername', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: `siteUsername=${encodeURIComponent(siteUsername)}`
        });

        if (!response.ok) throw new Error(`HTTP error! status: ${response.status}`);
        return await response.json();
    } catch (error) {
        console.error("Error validating guard:", error);
        return { isValid: false, message: "Network error validating guard" };
    }
}

async function getTodaysShift(guardId) {
    try {
        const response = await fetch(`/Guard/GetTodaysShift?guardId=${guardId}&date=${today}`);
        if (!response.ok) throw new Error(`HTTP error! status: ${response.status}`);
        return await response.json();
    } catch (error) {
        console.error("Error getting today's shift:", error);
        return { success: false, shift: null };
    }
}

async function saveCheckInToDatabase(checkinRecord) {
    try {
        const response = await fetch('/Guard/SaveCheckIn', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(checkinRecord)
        });

        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(`Server error: ${response.status} - ${errorText}`);
        }

        return await response.json();
    } catch (error) {
        console.error("Error saving to database:", error);
        return { success: false, message: error.message };
    }
}

async function getTodayGuardCheckIns(guardId) {
    try {
        const response = await fetch(`/Guard/GetTodayCheckIns?guardId=${guardId}&date=${today}`);
        return response.ok ? await response.json() : { success: false, checkins: [] };
    } catch (error) {
        console.error("Error getting check-ins:", error);
        return { success: false, checkins: [] };
    }
}

// QR CODE GENERATION
function generateQRCode() {
    if (!isValidGuard || !currentGuardData) {
        showMessage("Please validate your identity first.", "error");
        return;
    }

    const qrcodeDiv = document.getElementById("qrcode");
    const scannedCodeInput = document.getElementById("scannedCode");

    qrcodeDiv.innerHTML = "";
    scannedCodeInput.value = "";

    // Check if guard is off duty today
    if (currentShiftData && currentShiftData.shiftType === "Off") {
        showMessage("You are off duty today. Cannot check in/out.", "error");
        return;
    }

    // Check if already completed shift using session storage
    const todayCheckins = checkinData.filter(entry =>
        entry.guardId === currentGuardData.guardId && entry.date === today
    );

    const hasCheckedOut = todayCheckins.some(entry => entry.status === "Checked Out" || entry.status === "Late Departure");
    if (hasCheckedOut) {
        showMessage("You have already completed your shift today.", "error");
        return;
    }

    const token = generateShortCode();
    console.log("Generated token:", token);

    try {
        // Clear the QR code div
        qrcodeDiv.innerHTML = "";

        // Create container for QR code
        const container = document.createElement('div');
        container.className = 'qr-container';

        // Generate QR code using the qrcode-generator library
        const typeNumber = 4; // QR code type
        const errorCorrectionLevel = 'L'; // Error correction level

        // Check if QR code library is available
        if (typeof qrcode === 'undefined') {
            throw new Error("QR code library not loaded");
        }

        const qr = qrcode(typeNumber, errorCorrectionLevel);
        qr.addData(token);
        qr.make();

        // Create canvas element for QR code
        const canvas = document.createElement('canvas');
        const size = 200;
        canvas.width = size;
        canvas.height = size;
        canvas.style.border = '1px solid #eee';

        const ctx = canvas.getContext('2d');

        // Set background to white
        ctx.fillStyle = '#ffffff';
        ctx.fillRect(0, 0, size, size);

        // Set QR code color to black
        ctx.fillStyle = '#000000';

        // Get QR code modules and draw them
        const moduleCount = qr.getModuleCount();
        const tileSize = size / moduleCount;

        for (let row = 0; row < moduleCount; row++) {
            for (let col = 0; col < moduleCount; col++) {
                if (qr.isDark(row, col)) {
                    ctx.fillRect(
                        col * tileSize,
                        row * tileSize,
                        tileSize,
                        tileSize
                    );
                }
            }
        }

        // Add canvas to container
        container.appendChild(canvas);

        // Add to main QR code div
        qrcodeDiv.appendChild(container);

        // Add clear button
        const clearBtn = document.createElement('button');
        clearBtn.textContent = 'Clear QR Code';
        clearBtn.onclick = clearQRCode;
        clearBtn.style.marginTop = '15px';
        clearBtn.style.padding = '8px 16px';
        clearBtn.style.backgroundColor = '#6c757d';
        clearBtn.style.color = 'white';
        clearBtn.style.border = 'none';
        clearBtn.style.borderRadius = '4px';
        clearBtn.style.cursor = 'pointer';
        qrcodeDiv.appendChild(clearBtn);

        showMessage("QR Code generated successfully! Scan the code below.", "success");
        console.log("QR Code generated successfully with token:", token);

    } catch (error) {
        console.error("QR Code generation error:", error);
        // Fallback to manual code display only when QR generation fails
        generateManualCodeOnly(token, qrcodeDiv);
    }

    sessionStorage.setItem("lastToken", token);
    sessionStorage.setItem("tokenTime", new Date().getTime().toString());
    scannedCodeInput.focus();
}

function generateManualCodeOnly(token, qrcodeDiv) {
    const container = document.createElement('div');
    container.className = 'qr-container';
    container.style.textAlign = 'center';
    container.style.padding = '20px';
    container.style.border = '2px dashed #ccc';
    container.style.borderRadius = '10px';
    container.style.background = '#f9f9f9';

    const instruction = document.createElement('p');
    instruction.style.color = '#666';
    instruction.style.marginBottom = '15px';
    instruction.style.fontWeight = 'bold';
    instruction.textContent = 'QR Code unavailable. Please use this manual code:';

    const codeDisplay = document.createElement('div');
    codeDisplay.className = 'manual-code';
    codeDisplay.textContent = token;
    codeDisplay.style.fontSize = '24px';
    codeDisplay.style.fontWeight = 'bold';
    codeDisplay.style.letterSpacing = '3px';
    codeDisplay.style.margin = '10px 0';
    codeDisplay.style.padding = '15px';
    codeDisplay.style.background = '#000';
    codeDisplay.style.color = '#fff';
    codeDisplay.style.borderRadius = '5px';
    codeDisplay.style.fontFamily = "'Courier New', monospace";
    codeDisplay.style.textAlign = 'center';

    const note = document.createElement('p');
    note.style.color = '#888';
    note.style.fontSize = '12px';
    note.style.marginTop = '10px';
    note.textContent = 'Enter this code in the scanned code field below.';

    container.appendChild(instruction);
    container.appendChild(codeDisplay);
    container.appendChild(note);
    qrcodeDiv.appendChild(container);

    console.log("Manual code generated due to QR failure:", token);
    showMessage("Manual code generated. Please enter this code: " + token, "warning");
}

function clearQRCode() {
    const qrcodeDiv = document.getElementById("qrcode");
    qrcodeDiv.innerHTML = "<p style='color: #666;'>QR code will appear here after generation</p>";
    document.getElementById("statusMessage").textContent = "";
    document.getElementById("statusMessage").className = "";
    sessionStorage.removeItem("lastToken");
    sessionStorage.removeItem("tokenTime");
}

// MAIN VALIDATION FUNCTION - FIXED VERSION
async function validateGuard() {
    console.log('validateGuard function called');

    const siteUsername = document.getElementById("siteUsername").value.trim();
    const validationMessage = document.getElementById("validationMessage");
    const qrSection = document.getElementById("qrSection");
    const checkInBtn = document.getElementById("checkInBtn");
    const checkOutBtn = document.getElementById("checkOutBtn");

    // Reset messages and UI
    validationMessage.textContent = "";
    validationMessage.className = "";
    validationMessage.style.display = "none";
    showMessage("", "");
    qrSection.style.display = "none";
    checkInBtn.style.display = "none";
    checkOutBtn.style.display = "none";
    document.getElementById("qrcode").innerHTML = "<p style='color: #666;'>QR code will appear here after generation</p>";
    document.getElementById("scannedCode").value = "";

    if (!siteUsername) {
        showValidationMessage("Please enter your site username.", "error");
        return;
    }

    // Show loading state
    showValidationMessage("Validating... Please wait.", "info");

    const validateBtn = document.getElementById("validateBtn");
    const originalText = validateBtn.textContent;
    validateBtn.textContent = "Validating...";
    validateBtn.disabled = true;

    try {
        console.log('Calling validateGuardByUsername API');
        const result = await validateGuardByUsername(siteUsername);
        console.log('API result:', result);

        validateBtn.textContent = originalText;
        validateBtn.disabled = false;

        if (result.isValid) {
            currentGuardData = result.guardData;

            // Get today's shift information
            const shiftResult = await getTodaysShift(currentGuardData.guardId);
            currentShiftData = shiftResult.success ? shiftResult.shift : null;

            if (currentShiftData) {
                currentShiftType = currentShiftData.shiftType;

                // Check if guard is off duty
                if (currentShiftType === "Off") {
                    showValidationMessage(`Welcome, ${currentGuardData.fullName}! You are OFF DUTY today.`, "warning");
                    document.getElementById("currentGuardInfo").textContent =
                        `${currentGuardData.fullName} - OFF DUTY`;
                    document.getElementById("currentGuardInfo").style.display = "block";
                    qrSection.style.display = "none";
                    isValidGuard = true;
                    return;
                }
            } else {
                currentShiftType = determineShiftType();
            }

            // Get today's check-ins from database
            const todayCheckinsResult = await getTodayGuardCheckIns(currentGuardData.guardId);
            console.log('Today check-ins result:', todayCheckinsResult);

            const todayCheckins = todayCheckinsResult.success ? todayCheckinsResult.checkins : [];
            console.log('Today check-ins:', todayCheckins);

            // Check for check-in and check-out status
            const hasCheckedIn = todayCheckins.some(e =>
                e.status === "Present" || e.status === "Late Arrival"
            );
            const hasCheckedOut = todayCheckins.some(e =>
                e.status === "Checked Out" || e.status === "Late Departure"
            );

            console.log('Has checked in:', hasCheckedIn);
            console.log('Has checked out:', hasCheckedOut);

            // Update guard info display
            const shiftInfo = currentShiftData ?
                `${currentShiftType} Shift at ${currentShiftData.location || "Main Gate"}` :
                `${currentShiftType} Shift`;

            document.getElementById("currentGuardInfo").textContent =
                `${currentGuardData.fullName} - ${shiftInfo}`;
            document.getElementById("currentGuardInfo").style.display = "block";

            if (hasCheckedOut) {
                // Guard has already completed shift
                showValidationMessage(`Welcome, ${currentGuardData.fullName}! You have already completed your shift today.`, "success");
                qrSection.style.display = "none";
                checkInBtn.style.display = "none";
                checkOutBtn.style.display = "none";
                isValidGuard = false;
            } else if (hasCheckedIn) {
                // Guard has checked in but not checked out - SHOW CHECK OUT BUTTON
                showValidationMessage(`Welcome, ${currentGuardData.fullName}! You are checked in and can now check out.`, "success");
                checkInBtn.style.display = "none";
                checkOutBtn.style.display = "block";
                qrSection.style.display = "block";
                isValidGuard = true;

                // Update local session storage to reflect this state
                const localCheckinRecord = {
                    guardId: currentGuardData.guardId,
                    siteUsername: currentGuardData.siteUsername,
                    name: currentGuardData.fullName,
                    status: "Present",
                    date: today,
                    timestamp: new Date().toISOString()
                };

                // Check if this record already exists in local storage
                const existingIndex = checkinData.findIndex(entry =>
                    entry.guardId === currentGuardData.guardId &&
                    entry.date === today &&
                    (entry.status === "Present" || entry.status === "Late Arrival")
                );

                if (existingIndex === -1) {
                    checkinData.push(localCheckinRecord);
                    sessionStorage.setItem("checkinData", JSON.stringify(checkinData));
                }
            } else {
                // Guard has not checked in - show CHECK IN button
                showValidationMessage(`Welcome, ${currentGuardData.fullName}! You can check in now.`, "success");
                checkInBtn.style.display = "block";
                checkOutBtn.style.display = "none";
                qrSection.style.display = "block";
                isValidGuard = true;
            }

            sessionStorage.setItem("guardData", JSON.stringify(currentGuardData));
            if (currentShiftData) {
                sessionStorage.setItem("shiftData", JSON.stringify(currentShiftData));
            }
        } else {
            showValidationMessage(result.message || "Site username not recognized.", "error");
            qrSection.style.display = "none";
            document.getElementById("currentGuardInfo").style.display = "none";
            checkInBtn.style.display = "none";
            checkOutBtn.style.display = "none";
            isValidGuard = false;
        }
    } catch (error) {
        console.error("Error:", error);
        validateBtn.textContent = originalText;
        validateBtn.disabled = false;
        showValidationMessage("Network error. Please try again.", "error");
        qrSection.style.display = "none";
        document.getElementById("currentGuardInfo").style.display = "none";
        checkInBtn.style.display = "none";
        checkOutBtn.style.display = "none";
        isValidGuard = false;
    }
}

// CHECK IN FUNCTION
async function checkIn() {
    if (!isValidGuard || !currentGuardData) {
        showMessage("Please validate your identity first.", "error");
        return;
    }

    // Check if off duty
    if (currentShiftData && currentShiftData.shiftType === "Off") {
        showMessage("You are off duty today. Cannot check in.", "error");
        return;
    }

    const expectedTime = getExpectedTime(currentShiftType, 'checkin');
    await verifyScan("Present", expectedTime);
}

// CHECK OUT FUNCTION
async function checkOut() {
    if (!isValidGuard || !currentGuardData) {
        showMessage("Please validate your identity first.", "error");
        return;
    }

    // Check if off duty
    if (currentShiftData && currentShiftData.shiftType === "Off") {
        showMessage("You are off duty today. Cannot check out.", "error");
        return;
    }

    const expectedTime = getExpectedTime(currentShiftType, 'checkout');
    await verifyScan("Checked Out", expectedTime);
}

// VERIFY SCAN FUNCTION
async function verifyScan(statusType, expectedTime) {
    const scanned = document.getElementById("scannedCode").value.trim().toUpperCase();

    if (!scanned) {
        showMessage("Please enter the scanned code.", "error");
        return;
    }

    const exactToken = sessionStorage.getItem("lastToken");
    const tokenTime = parseInt(sessionStorage.getItem("tokenTime") || "0");
    const tokenAge = (new Date().getTime() - tokenTime) / 1000 / 60;

    if (!exactToken) {
        showMessage("Please generate a QR code first.", "error");
        return;
    }

    if (tokenAge > 5) {
        showMessage("QR code expired. Please generate a new one.", "error");
        sessionStorage.removeItem("lastToken");
        sessionStorage.removeItem("tokenTime");
        return;
    }

    if (scanned !== exactToken) {
        showMessage("Invalid QR code. Please scan the correct code.", "error");
        return;
    }

    const currentTime = getCurrentTimeString();
    const isLate = currentTime > expectedTime;

    let statusWithTiming = statusType;
    if (isLate) {
        statusWithTiming = statusType === "Present" ? "Late Arrival" : "Late Departure";
    }

    const checkinRecord = {
        guardId: currentGuardData.guardId,
        siteUsername: currentGuardData.siteUsername,
        status: statusWithTiming,
        expectedTime: expectedTime,
        actualTime: currentTime,
        isLate: isLate,
        rosterId: currentShiftData ? currentShiftData.rosterId : null
    };

    try {
        // Show processing message
        showMessage("Processing... Please wait.", "info");

        const result = await saveCheckInToDatabase(checkinRecord);

        if (result.success) {
            // For local storage
            const localRecord = {
                ...checkinRecord,
                name: currentGuardData.fullName,
                date: today,
                time: currentTime,
                timestamp: new Date().toISOString()
            };

            // Remove any existing check-in for today before adding new one
            checkinData = checkinData.filter(entry =>
                !(entry.guardId === currentGuardData.guardId && entry.date === today)
            );

            checkinData.push(localRecord);
            sessionStorage.setItem("checkinData", JSON.stringify(checkinData));

            let statusMessage = `${statusType} recorded successfully at ${currentTime}!`;
            if (isLate) {
                statusMessage += ` (${statusWithTiming})`;
            }

            showMessage(statusMessage, isLate ? "warning" : "success");
            document.getElementById("scannedCode").value = "";

            // Clear the QR code
            clearQRCode();

            if (statusType === "Present") {
                // After check-in, show check-out button
                document.getElementById("checkInBtn").style.display = "none";
                document.getElementById("checkOutBtn").style.display = "block";
                showMessage("You are checked in. You can now check out when your shift ends.", "success");
            } else {
                // After check-out, hide everything
                document.getElementById("qrSection").style.display = "none";
                document.getElementById("checkInBtn").style.display = "none";
                document.getElementById("checkOutBtn").style.display = "none";
                isValidGuard = false;
                showMessage("Shift completed successfully! Thank you.", "success");
            }
        } else {
            showMessage(result.message || "Error saving to database.", "error");
        }
    } catch (error) {
        showMessage("Error saving to database. Please try again.", "error");
    }

    // Clear token regardless of success/failure
    sessionStorage.removeItem("lastToken");
    sessionStorage.removeItem("tokenTime");
}

// RESET FORM FUNCTION
function resetForm() {
    sessionStorage.removeItem("guardData");
    sessionStorage.removeItem("shiftData");
    sessionStorage.removeItem("lastToken");
    sessionStorage.removeItem("tokenTime");
    sessionStorage.removeItem("checkinData");
    currentGuardData = null;
    currentShiftData = null;
    isValidGuard = false;
    checkinData = [];

    document.getElementById("siteUsername").value = "";
    document.getElementById("validationMessage").textContent = "";
    document.getElementById("validationMessage").className = "";
    document.getElementById("validationMessage").style.display = "none";
    document.getElementById("qrSection").style.display = "none";
    document.getElementById("qrcode").innerHTML = "<p style='color: #666;'>QR code will appear here after generation</p>";
    document.getElementById("scannedCode").value = "";
    document.getElementById("statusMessage").textContent = "";
    document.getElementById("statusMessage").className = "";
    document.getElementById("statusMessage").style.display = "none";
    document.getElementById("currentGuardInfo").textContent = "";
    document.getElementById("currentGuardInfo").style.display = "none";
    document.getElementById("checkInBtn").style.display = "none";
    document.getElementById("checkOutBtn").style.display = "none";

    // Focus back to username field
    document.getElementById("siteUsername").focus();
}

// EVENT LISTENERS
document.addEventListener('DOMContentLoaded', function () {
    console.log('DOM loaded - attaching event listeners');

    // Load existing data
    const savedGuardData = sessionStorage.getItem("guardData");
    const savedShiftData = sessionStorage.getItem("shiftData");

    if (savedGuardData) {
        try {
            currentGuardData = JSON.parse(savedGuardData);
            if (savedShiftData) {
                currentShiftData = JSON.parse(savedShiftData);
                currentShiftType = currentShiftData.shiftType;
            } else {
                currentShiftType = determineShiftType();
            }

            document.getElementById("currentGuardInfo").textContent =
                `${currentGuardData.fullName} - ${currentShiftData && currentShiftData.shiftType === "Off" ?
                    "OFF DUTY" : currentShiftType + " Shift"}`;
            document.getElementById("currentGuardInfo").style.display = "block";

            // Auto-show appropriate section
            validateGuard();
        } catch (e) {
            console.error("Error loading saved data:", e);
            resetForm();
        }
    }

    // Set up event listeners
    document.getElementById('validateBtn').addEventListener('click', validateGuard);
    document.getElementById('checkInBtn').addEventListener('click', checkIn);
    document.getElementById('checkOutBtn').addEventListener('click', checkOut);
    document.getElementById('generateQRBtn').addEventListener('click', generateQRCode);
    document.getElementById('resetBtn').addEventListener('click', resetForm);
    document.getElementById('backToDashboardBtn').addEventListener('click', goToDashboard);

    // Enter key handlers
    document.getElementById("siteUsername").addEventListener("keypress", function (event) {
        if (event.key === "Enter") {
            event.preventDefault();
            validateGuard();
        }
    });

    document.getElementById("scannedCode").addEventListener("keypress", function (event) {
        if (event.key === "Enter") {
            event.preventDefault();
            if (document.getElementById("checkInBtn").style.display !== "none") {
                checkIn();
            } else if (document.getElementById("checkOutBtn").style.display !== "none") {
                checkOut();
            }
        }
    });

    // Hide buttons initially
    document.getElementById('checkInBtn').style.display = 'none';
    document.getElementById('checkOutBtn').style.display = 'none';
    document.getElementById('qrSection').style.display = 'none';
    document.getElementById('currentGuardInfo').style.display = 'none';

    console.log('Event listeners attached successfully');
});