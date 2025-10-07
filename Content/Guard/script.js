// script.js - Updated for SAST time zone and flexible check-out

// Global variables
let isValidGuard = false;
let currentGuardData = null;
let currentShiftType = null;
let currentShiftData = null;

// Configuration with SAST timing
const DAY_SHIFT_CHECKIN_START = "06:00"; // 6:00 AM SAST
const DAY_SHIFT_CHECKIN_END = "17:00";   // 5:00 PM SAST
const DAY_SHIFT_CHECKOUT_START = "07:00"; // 7:00 AM SAST (1 hour after check-in start)
const DAY_SHIFT_CHECKOUT_END = "23:00";   // 11:00 PM SAST (flexible for demo)

const NIGHT_SHIFT_CHECKIN_START = "18:00"; // 6:00 PM SAST
const NIGHT_SHIFT_CHECKIN_END = "05:00";   // 5:00 AM SAST next day
const NIGHT_SHIFT_CHECKOUT_START = "19:00"; // 7:00 PM SAST (1 hour after check-in start)
const NIGHT_SHIFT_CHECKOUT_END = "11:00";   // 11:00 AM SAST next day

// Utility functions
function generateShortCode() {
    return Math.random().toString(36).substring(2, 8).toUpperCase();
}

function getCurrentSASTTime() {
    // Create date in UTC and convert to SAST (UTC+2)
    const now = new Date();
    const sastOffset = 2 * 60; // SAST is UTC+2 in minutes
    const localOffset = now.getTimezoneOffset(); // in minutes
    const sastTime = new Date(now.getTime() + (sastOffset + localOffset) * 60000);

    return sastTime;
}

function getCurrentTimeString() {
    const sastTime = getCurrentSASTTime();
    return sastTime.toTimeString().split(' ')[0].substring(0, 5);
}

function getCurrentHoursMinutes() {
    const sastTime = getCurrentSASTTime();
    return {
        hours: sastTime.getHours(),
        minutes: sastTime.getMinutes(),
        totalMinutes: sastTime.getHours() * 60 + sastTime.getMinutes()
    };
}

function determineShiftType() {
    const { hours } = getCurrentHoursMinutes();
    return (hours >= 6 && hours < 18) ? 'Day' : 'Night';
}

function getExpectedTime(shiftType, action) {
    if (shiftType === 'Day') {
        return action === 'checkin' ? DAY_SHIFT_CHECKIN_START : DAY_SHIFT_CHECKOUT_START;
    } else {
        return action === 'checkin' ? NIGHT_SHIFT_CHECKIN_START : NIGHT_SHIFT_CHECKOUT_START;
    }
}

function showMessage(text, type) {
    const element = document.getElementById("statusMessage");
    element.textContent = text;
    element.className = `message ${type}`;
    element.style.display = "block";

    // Auto-hide success messages after 5 seconds
    if (type === 'success') {
        setTimeout(() => {
            element.style.display = "none";
        }, 5000);
    }
}

function showValidationMessage(text, type) {
    const element = document.getElementById("validationMessage");
    element.textContent = text;
    element.className = `message ${type}`;
    element.style.display = "block";
}

function updateGuardInfo(message) {
    document.getElementById("currentGuardInfo").textContent = message;
}

// Navigation function
function goToDashboard() {
    window.location.href = '/Guard/Dashboard';
}

// API CALLS (same as before)
async function getCurrentGuardInfo() {
    try {
        const response = await fetch('/Guard/GetCurrentGuardInfo', {
            method: 'GET',
            headers: {
                'Content-Type': 'application/json',
                'X-Requested-With': 'XMLHttpRequest'
            }
        });

        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const result = await response.json();
        return result;
    } catch (error) {
        console.error("Error getting guard info:", error);
        return { success: false, message: "Network error getting guard information" };
    }
}

async function getTodaysShift(guardId) {
    try {
        const today = new Date().toISOString().split('T')[0];
        const response = await fetch(`/Guard/GetTodaysShift?guardId=${guardId}&date=${today}`, {
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });

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
            headers: {
                'Content-Type': 'application/json',
                'X-Requested-With': 'XMLHttpRequest'
            },
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

// Get today's check-ins from database
async function getTodayGuardCheckIns(guardId) {
    try {
        const today = new Date().toISOString().split('T')[0];
        const response = await fetch(`/Guard/GetTodayCheckIns?guardId=${guardId}&date=${today}`, {
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        });

        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const result = await response.json();
        return result;
    } catch (error) {
        console.error("Error getting check-ins:", error);
        return { success: false, checkins: [] };
    }
}

// Get guard's current status from database
async function getGuardCurrentStatus(guardId) {
    try {
        const result = await getTodayGuardCheckIns(guardId);

        if (!result.success) {
            console.log('Failed to get check-ins:', result.message);
            return { hasCheckedIn: false, hasCheckedOut: false };
        }

        const checkIns = result.checkins || [];

        // Find the latest check-in (Present/Late Arrival)
        const checkInRecords = checkIns.filter(ci =>
            ci.status === "Present" || ci.status === "Late Arrival"
        );
        const latestCheckIn = checkInRecords.length > 0 ?
            checkInRecords.sort((a, b) => new Date(b.time) - new Date(a.time))[0] : null;

        // Find the latest check-out (Checked Out/Late Departure)
        const checkOutRecords = checkIns.filter(ci =>
            ci.status === "Checked Out" || ci.status === "Late Departure"
        );
        const latestCheckOut = checkOutRecords.length > 0 ?
            checkOutRecords.sort((a, b) => new Date(b.time) - new Date(a.time))[0] : null;

        const status = {
            hasCheckedIn: !!latestCheckIn,
            hasCheckedOut: !!latestCheckOut,
            latestCheckIn: latestCheckIn,
            latestCheckOut: latestCheckOut
        };

        return status;
    } catch (error) {
        console.error("Error getting guard current status:", error);
        return { hasCheckedIn: false, hasCheckedOut: false };
    }
}

// UPDATED: Enhanced timing validation for SAST
function validateCheckInTiming(shiftType, action) {
    const { hours, minutes, totalMinutes } = getCurrentHoursMinutes();
    const currentTime = hours * 100 + minutes; // Convert to HHMM format

    console.log(`Validating ${action} for ${shiftType} shift at ${hours}:${minutes} SAST`);

    if (action === 'checkin') {
        if (shiftType === 'Day') {
            // Day shift check-in in SAST: 6 AM (360) to 5 PM (1020)
            if (totalMinutes >= 360 && totalMinutes <= 1020) {
                return { valid: true, message: '' };
            } else {
                return {
                    valid: false,
                    message: 'Day shift check-in is only allowed between 6:00 AM and 5:00 PM SAST'
                };
            }
        } else if (shiftType === 'Night') {
            // Night shift check-in in SAST: 6 PM (1080) to 5 AM (300) next day
            if (totalMinutes >= 1080 || totalMinutes <= 300) {
                return { valid: true, message: '' };
            } else {
                return {
                    valid: false,
                    message: 'Night shift check-in is only allowed between 6:00 PM and 5:00 AM SAST'
                };
            }
        }
    } else if (action === 'checkout') {
        // For checkout, we'll rely on server-side validation for the 1-hour minimum
        // Just do basic time window validation here
        if (shiftType === 'Day') {
            // Day shift check-out in SAST: 7 AM (420) to 11 PM (1380)
            if (totalMinutes >= 420 && totalMinutes <= 1380) {
                return { valid: true, message: '' };
            } else {
                return {
                    valid: false,
                    message: 'Day shift check-out is allowed between 7:00 AM and 11:00 PM SAST'
                };
            }
        } else if (shiftType === 'Night') {
            // Night shift check-out in SAST: 7 PM (1140) to 11 AM (660) next day
            if ((totalMinutes >= 1140 && totalMinutes <= 1440) || (totalMinutes >= 0 && totalMinutes <= 660)) {
                return { valid: true, message: '' };
            } else {
                return {
                    valid: false,
                    message: 'Night shift check-out is allowed between 7:00 PM and 11:00 AM SAST'
                };
            }
        }
    }

    return { valid: true, message: '' };
}

// UPDATED: Enhanced checkIn function with SAST timing validation
async function checkIn() {
    if (!isValidGuard || !currentGuardData) {
        showMessage("Please wait for system to initialize.", "error");
        return;
    }

    // Check if off duty
    if (currentShiftData && currentShiftData.shiftType === "Off") {
        showMessage("You are off duty today. Cannot check in.", "error");
        return;
    }

    // Validate check-in timing in SAST
    const shiftType = currentShiftData ? currentShiftData.shiftType : determineShiftType();
    const timingValidation = validateCheckInTiming(shiftType, 'checkin');

    if (!timingValidation.valid) {
        showMessage(timingValidation.message, "error");
        return;
    }

    const expectedTime = getExpectedTime(shiftType, 'checkin');

    // Show current SAST time for confirmation
    const currentSAST = getCurrentTimeString();
    showMessage(`Current time: ${currentSAST} SAST - Proceeding with check-in...`, "info");

    await verifyScan("Present", expectedTime);
}

// UPDATED: Enhanced checkOut function with SAST timing validation
async function checkOut() {
    if (!isValidGuard || !currentGuardData) {
        showMessage("Please wait for system to initialize.", "error");
        return;
    }

    // Check if off duty
    if (currentShiftData && currentShiftData.shiftType === "Off") {
        showMessage("You are off duty today. Cannot check out.", "error");
        return;
    }

    // First check if guard has checked in today
    const currentStatus = await getGuardCurrentStatus(currentGuardData.guardId);
    if (!currentStatus.hasCheckedIn) {
        showMessage("You need to check in first before checking out.", "error");
        return;
    }

    // Validate check-out timing in SAST
    const shiftType = currentShiftData ? currentShiftData.shiftType : determineShiftType();
    const timingValidation = validateCheckInTiming(shiftType, 'checkout');

    if (!timingValidation.valid) {
        showMessage(timingValidation.message, "error");
        return;
    }

    const expectedTime = getExpectedTime(shiftType, 'checkout');

    // Show current SAST time for confirmation
    const currentSAST = getCurrentTimeString();
    showMessage(`Current time: ${currentSAST} SAST - Proceeding with check-out...`, "info");

    await verifyScan("Checked Out", expectedTime);
}

// UPDATED: Initialize function with SAST time display
async function initializeGuardSystem() {
    console.log('=== INITIALIZING GUARD SYSTEM (SAST) ===');

    try {
        updateGuardInfo("Loading your information...");

        // Get current guard information
        const result = await getCurrentGuardInfo();

        if (!result.success) {
            updateGuardInfo("Error: Could not load guard information. Please refresh the page.");
            showValidationMessage(result.message || "Failed to load guard information", "error");
            return;
        }

        currentGuardData = result.guardData;
        console.log('Guard data loaded:', currentGuardData);

        // Get today's shift information
        const shiftResult = await getTodaysShift(currentGuardData.guardId);
        currentShiftData = shiftResult.success ? shiftResult.shift : null;

        if (currentShiftData) {
            currentShiftType = currentShiftData.shiftType;
            console.log('Shift data found:', currentShiftData);

            // Check if guard is off duty
            if (currentShiftType === "Off") {
                updateGuardInfo(`${currentGuardData.fullName} - OFF DUTY TODAY`);
                showValidationMessage("You are off duty today. Cannot check in/out.", "warning");

                // Disable both buttons for off-duty guards
                document.getElementById("checkInBtn").disabled = true;
                document.getElementById("checkOutBtn").disabled = true;
                isValidGuard = true;
                return;
            }
        } else {
            currentShiftType = determineShiftType();
            console.log('No shift data, using default:', currentShiftType);
        }

        // Get current status
        const currentStatus = await getGuardCurrentStatus(currentGuardData.guardId);
        console.log('Current status:', currentStatus);

        // Update guard info display with SAST time
        const shiftInfo = currentShiftData ?
            `${currentShiftType} Shift at ${currentShiftData.location || "Main Gate"}` :
            `${currentShiftType} Shift`;

        const currentSAST = getCurrentTimeString();
        const { hours, minutes } = getCurrentHoursMinutes();
        updateGuardInfo(`${currentGuardData.fullName} - ${shiftInfo} - Current Time: ${currentSAST} SAST`);
        isValidGuard = true;

        // Enable/disable buttons based on current status
        document.getElementById("checkInBtn").disabled = currentStatus.hasCheckedIn;
        document.getElementById("checkOutBtn").disabled = !currentStatus.hasCheckedIn || currentStatus.hasCheckedOut;

        // Show status information
        if (currentStatus.hasCheckedOut) {
            showValidationMessage("You have already completed your shift today.", "success");
        } else if (currentStatus.hasCheckedIn) {
            showValidationMessage("You are checked in and can check out now.", "success");
        } else {
            showValidationMessage("Ready to check in. You can check in between 6:00 AM - 5:00 PM SAST for day shift.", "success");
        }

        console.log('=== GUARD SYSTEM INITIALIZED (SAST) ===');

    } catch (error) {
        console.error("Initialization error:", error);
        updateGuardInfo("Error: Failed to initialize system");
        showValidationMessage("System error. Please refresh the page.", "error");
    }
}

// UPDATED: Enhanced verifyScan function with SAST time display
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
    const currentSAST = getCurrentSASTTime();

    // Convert expected time to minutes for comparison
    const [expectedHours, expectedMinutes] = expectedTime.split(':').map(Number);
    const expectedTotalMinutes = expectedHours * 60 + expectedMinutes;

    const { totalMinutes } = getCurrentHoursMinutes();

    const isLate = totalMinutes > expectedTotalMinutes;

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
        showMessage("Processing... Please wait.", "info");

        const result = await saveCheckInToDatabase(checkinRecord);

        if (result.success) {
            let statusMessage = `${statusType} recorded successfully at ${currentTime} SAST!`;
            if (isLate) {
                statusMessage += ` (${statusWithTiming})`;
            }

            showMessage(statusMessage, isLate ? "warning" : "success");
            document.getElementById("scannedCode").value = "";

            // Clear the QR code
            clearQRCode();

            // Refresh the system to update button states
            setTimeout(() => {
                refreshSystem();
            }, 2000);

            if (statusType === "Present") {
                showMessage("Check-in successful! You can check out after working for at least 1 hour.", "success");
            } else {
                showMessage("Check-out successful! Your hours have been recorded for payroll.", "success");
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

// QR CODE GENERATION (same as before)
function generateQRCode() {
    if (!isValidGuard || !currentGuardData) {
        showMessage("Please wait for system to initialize.", "error");
        return;
    }

    const qrcodeDiv = document.getElementById("qrcode");
    const scannedCodeInput = document.getElementById("scannedCode");
    const clearQRBtn = document.getElementById("clearQRBtn");

    qrcodeDiv.innerHTML = "";
    scannedCodeInput.value = "";

    // Check if guard is off duty today
    if (currentShiftData && currentShiftData.shiftType === "Off") {
        showMessage("You are off duty today. Cannot check in/out.", "error");
        return;
    }

    // Generate QR code immediately
    generateQRCodeInternal();
}

function generateQRCodeInternal() {
    const token = generateShortCode();
    console.log("Generated token:", token);

    const qrcodeDiv = document.getElementById("qrcode");
    const clearQRBtn = document.getElementById("clearQRBtn");

    try {
        qrcodeDiv.innerHTML = "";

        // Create container for QR code
        const container = document.createElement('div');
        container.className = 'qr-container';

        // Generate QR code
        const typeNumber = 4;
        const errorCorrectionLevel = 'L';

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

        container.appendChild(canvas);
        qrcodeDiv.appendChild(container);

        // Show clear button
        clearQRBtn.style.display = 'inline-block';

        showMessage("QR Code generated successfully! Scan the code below.", "success");

    } catch (error) {
        console.error("QR Code generation error:", error);
        generateManualCodeOnly(token, qrcodeDiv);
    }

    sessionStorage.setItem("lastToken", token);
    sessionStorage.setItem("tokenTime", new Date().getTime().toString());
    document.getElementById("scannedCode").focus();
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

    showMessage("Manual code generated. Please enter this code: " + token, "warning");
}

function clearQRCode() {
    const qrcodeDiv = document.getElementById("qrcode");
    const clearQRBtn = document.getElementById("clearQRBtn");

    qrcodeDiv.innerHTML = "<p style='color: #666;'>QR code will appear here after generation</p>";
    document.getElementById("statusMessage").textContent = "";
    document.getElementById("statusMessage").className = "";
    clearQRBtn.style.display = 'none';
    sessionStorage.removeItem("lastToken");
    sessionStorage.removeItem("tokenTime");
}

// REFRESH FUNCTION
function refreshSystem() {
    // Clear session storage
    sessionStorage.removeItem("guardData");
    sessionStorage.removeItem("shiftData");
    sessionStorage.removeItem("lastToken");
    sessionStorage.removeItem("tokenTime");

    // Reset variables
    currentGuardData = null;
    currentShiftData = null;
    isValidGuard = false;

    // Clear UI
    document.getElementById("qrcode").innerHTML = "<p style='color: #666;'>QR code will appear here after generation</p>";
    document.getElementById("scannedCode").value = "";
    document.getElementById("statusMessage").textContent = "";
    document.getElementById("statusMessage").className = "";
    document.getElementById("statusMessage").style.display = "none";
    document.getElementById("validationMessage").textContent = "";
    document.getElementById("validationMessage").className = "";
    document.getElementById("validationMessage").style.display = "none";
    document.getElementById("clearQRBtn").style.display = 'none';

    // Disable buttons temporarily
    document.getElementById("checkInBtn").disabled = true;
    document.getElementById("checkOutBtn").disabled = true;

    // Re-initialize the system
    initializeGuardSystem();
}

// EVENT LISTENERS
document.addEventListener('DOMContentLoaded', function () {
    console.log('DOM loaded - attaching event listeners');

    // Set up event listeners
    document.getElementById('checkInBtn').addEventListener('click', checkIn);
    document.getElementById('checkOutBtn').addEventListener('click', checkOut);
    document.getElementById('generateQRBtn').addEventListener('click', generateQRCode);
    document.getElementById('clearQRBtn').addEventListener('click', clearQRCode);
    document.getElementById('resetBtn').addEventListener('click', refreshSystem);
    document.getElementById('backToDashboardBtn').addEventListener('click', goToDashboard);

    // Enter key handler for scanned code
    document.getElementById("scannedCode").addEventListener("keypress", function (event) {
        if (event.key === "Enter") {
            event.preventDefault();
            // Try check-in first, then check-out
            if (!document.getElementById("checkInBtn").disabled) {
                checkIn();
            } else if (!document.getElementById("checkOutBtn").disabled) {
                checkOut();
            }
        }
    });

    // Initialize the system immediately
    initializeGuardSystem();

    console.log('Event listeners attached successfully');
});