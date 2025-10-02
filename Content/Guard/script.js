// script.js - Fixed Guard Check-In System with Always Visible Buttons

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
const MIN_CHECKIN_DURATION_MINUTES = 60; // 1 hour minimum

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

// Get today's check-ins from database
async function getTodayGuardCheckIns(guardId) {
    try {
        console.log(`Fetching check-ins for guard ${guardId} on ${today}`);
        const response = await fetch(`/Guard/GetTodayCheckIns?guardId=${guardId}&date=${today}`);

        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const result = await response.json();
        console.log('Check-ins API response:', result);
        return result;
    } catch (error) {
        console.error("Error getting check-ins:", error);
        return { success: false, checkins: [] };
    }
}

// Get guard's current status from database
async function getGuardCurrentStatus(guardId) {
    try {
        console.log(`Getting current status for guard: ${guardId}`);
        const result = await getTodayGuardCheckIns(guardId);

        if (!result.success) {
            console.log('Failed to get check-ins:', result.message);
            return { hasCheckedIn: false, hasCheckedOut: false, latestCheckIn: null, latestCheckOut: null };
        }

        const checkIns = result.checkins || [];
        console.log('Raw check-ins data:', checkIns);

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
            latestCheckOut: latestCheckOut,
            allCheckIns: checkIns
        };

        console.log('Calculated status:', status);
        return status;
    } catch (error) {
        console.error("Error getting guard current status:", error);
        return { hasCheckedIn: false, hasCheckedOut: false, latestCheckIn: null, latestCheckOut: null };
    }
}

// Check if guard can check out (minimum 1 hour after check-in)
async function canGuardCheckOut(guardId) {
    try {
        const status = await getGuardCurrentStatus(guardId);

        if (!status.hasCheckedIn || !status.latestCheckIn) {
            console.log('Cannot check out: No check-in found');
            return false;
        }

        if (status.hasCheckedOut) {
            console.log('Cannot check out: Already checked out');
            return false;
        }

        // Calculate time difference
        const checkInTime = new Date(status.latestCheckIn.time);
        const currentTime = new Date();
        const timeDiffMinutes = (currentTime - checkInTime) / (1000 * 60);

        console.log(`Check-in time: ${checkInTime}, Current time: ${currentTime}, Difference: ${timeDiffMinutes} minutes, Required: ${MIN_CHECKIN_DURATION_MINUTES} minutes`);

        const canCheckOut = timeDiffMinutes >= MIN_CHECKIN_DURATION_MINUTES;
        console.log(`Can check out: ${canCheckOut}`);

        return canCheckOut;
    } catch (error) {
        console.error("Error checking check-out eligibility:", error);
        return false;
    }
}

// REVISED VALIDATION FUNCTION WITH ALWAYS VISIBLE BUTTONS
async function validateGuard() {
    console.log('=== VALIDATE GUARD STARTED ===');

    const siteUsername = document.getElementById("siteUsername").value.trim();
    const validationMessage = document.getElementById("validationMessage");
    const qrSection = document.getElementById("qrSection");
    const checkInBtn = document.getElementById("checkInBtn");
    const checkOutBtn = document.getElementById("checkOutBtn");

    // Reset UI
    validationMessage.textContent = "";
    validationMessage.className = "";
    validationMessage.style.display = "none";
    showMessage("", "");
    qrSection.style.display = "none";

    // Reset button states but keep them visible
    checkInBtn.disabled = false;
    checkOutBtn.disabled = false;

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
        console.log('Step 1: Validating guard username');
        const result = await validateGuardByUsername(siteUsername);
        console.log('Username validation result:', result);

        if (!result.isValid) {
            showValidationMessage(result.message || "Site username not recognized.", "error");
            validateBtn.textContent = originalText;
            validateBtn.disabled = false;
            return;
        }

        currentGuardData = result.guardData;
        console.log('Guard data:', currentGuardData);

        // Step 2: Get today's shift information
        console.log('Step 2: Getting shift information');
        const shiftResult = await getTodaysShift(currentGuardData.guardId);
        currentShiftData = shiftResult.success ? shiftResult.shift : null;

        if (currentShiftData) {
            currentShiftType = currentShiftData.shiftType;
            console.log('Shift data found:', currentShiftData);

            // Check if guard is off duty
            if (currentShiftType === "Off") {
                showValidationMessage(`Welcome, ${currentGuardData.fullName}! You are OFF DUTY today.`, "warning");
                document.getElementById("currentGuardInfo").textContent = `${currentGuardData.fullName} - OFF DUTY`;
                document.getElementById("currentGuardInfo").style.display = "block";
                qrSection.style.display = "block";

                // Disable both buttons for off-duty guards
                checkInBtn.disabled = true;
                checkOutBtn.disabled = true;
                isValidGuard = true;

                validateBtn.textContent = originalText;
                validateBtn.disabled = false;
                return;
            }
        } else {
            currentShiftType = determineShiftType();
            console.log('No shift data, using default:', currentShiftType);
        }

        // Step 3: Get current status from DATABASE
        console.log('Step 3: Getting current status from database');
        const currentStatus = await getGuardCurrentStatus(currentGuardData.guardId);
        console.log('Current status from database:', currentStatus);

        // Update guard info display
        const shiftInfo = currentShiftData ?
            `${currentShiftType} Shift at ${currentShiftData.location || "Main Gate"}` :
            `${currentShiftType} Shift`;

        document.getElementById("currentGuardInfo").textContent = `${currentGuardData.fullName} - ${shiftInfo}`;
        document.getElementById("currentGuardInfo").style.display = "block";

        // Step 4: Update button states based on DATABASE status (BOTH BUTTONS ALWAYS VISIBLE)
        qrSection.style.display = "block";
        isValidGuard = true;

        if (currentStatus.hasCheckedOut) {
            // Guard has already completed shift
            console.log('Status: Already checked out');
            showValidationMessage(`Welcome, ${currentGuardData.fullName}! You have already completed your shift today.`, "success");
            checkInBtn.disabled = true;
            checkOutBtn.disabled = true;
        } else if (currentStatus.hasCheckedIn) {
            // Guard has checked in but not checked out
            console.log('Status: Checked in, can check out');

            // Check if minimum time has passed for check-out
            const canCheckOut = await canGuardCheckOut(currentGuardData.guardId);

            if (canCheckOut) {
                showValidationMessage(`Welcome, ${currentGuardData.fullName}! You are checked in and can now check out.`, "success");
                checkInBtn.disabled = true; // Can't check in again
                checkOutBtn.disabled = false; // Can check out
            } else {
                // Calculate remaining time
                const checkInTime = new Date(currentStatus.latestCheckIn.time);
                const timeDiffMinutes = (new Date() - checkInTime) / (1000 * 60);
                const remainingMinutes = Math.ceil(MIN_CHECKIN_DURATION_MINUTES - timeDiffMinutes);

                showValidationMessage(`Welcome, ${currentGuardData.fullName}! You are checked in. You must wait ${remainingMinutes} more minutes before checking out.`, "warning");
                checkInBtn.disabled = true; // Can't check in again
                checkOutBtn.disabled = true; // Can't check out yet
            }
        } else {
            // Guard has not checked in
            console.log('Status: Not checked in, can check in');
            showValidationMessage(`Welcome, ${currentGuardData.fullName}! You can check in now.`, "success");
            checkInBtn.disabled = false; // Can check in
            checkOutBtn.disabled = true; // Can't check out without checking in first
        }

        // Save to session storage
        sessionStorage.setItem("guardData", JSON.stringify(currentGuardData));
        if (currentShiftData) {
            sessionStorage.setItem("shiftData", JSON.stringify(currentShiftData));
        }

        console.log('=== VALIDATION COMPLETED ===');

    } catch (error) {
        console.error("Validation error:", error);
        showValidationMessage("Network error. Please try again.", "error");
        qrSection.style.display = "none";
        document.getElementById("currentGuardInfo").style.display = "none";
        checkInBtn.disabled = true;
        checkOutBtn.disabled = true;
        isValidGuard = false;
    } finally {
        validateBtn.textContent = originalText;
        validateBtn.disabled = false;
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

    // Double-check current status
    getGuardCurrentStatus(currentGuardData.guardId).then(status => {
        if (status.hasCheckedOut) {
            showMessage("You have already completed your shift today.", "error");
            return;
        }

        // Generate QR code
        generateQRCodeInternal();
    }).catch(error => {
        console.error("Error checking status:", error);
        generateQRCodeInternal();
    });
}

function generateQRCodeInternal() {
    const token = generateShortCode();
    console.log("Generated token:", token);

    const qrcodeDiv = document.getElementById("qrcode");

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
    qrcodeDiv.innerHTML = "<p style='color: #666;'>QR code will appear here after generation</p>";
    document.getElementById("statusMessage").textContent = "";
    document.getElementById("statusMessage").className = "";
    sessionStorage.removeItem("lastToken");
    sessionStorage.removeItem("tokenTime");
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

    // Check if already checked in
    const status = await getGuardCurrentStatus(currentGuardData.guardId);
    if (status.hasCheckedIn) {
        showMessage("You have already checked in today.", "error");
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

    // Check if minimum time has passed
    const canCheckOut = await canGuardCheckOut(currentGuardData.guardId);
    if (!canCheckOut) {
        showMessage("You must wait at least 1 hour after checking in before you can check out.", "error");
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
        showMessage("Processing... Please wait.", "info");

        const result = await saveCheckInToDatabase(checkinRecord);

        if (result.success) {
            let statusMessage = `${statusType} recorded successfully at ${currentTime}!`;
            if (isLate) {
                statusMessage += ` (${statusWithTiming})`;
            }

            showMessage(statusMessage, isLate ? "warning" : "success");
            document.getElementById("scannedCode").value = "";

            // Clear the QR code
            clearQRCode();

            // Update button states after successful action
            if (statusType === "Present") {
                // After check-in, disable check-in button and enable check-out (if time allows)
                document.getElementById("checkInBtn").disabled = true;

                // Check if can check out immediately
                const canCheckOut = await canGuardCheckOut(currentGuardData.guardId);
                document.getElementById("checkOutBtn").disabled = !canCheckOut;

                if (!canCheckOut) {
                    showMessage("You are checked in. You must wait at least 1 hour before checking out.", "warning");
                } else {
                    showMessage("You are checked in. You can now check out.", "success");
                }

                // Re-validate after 1 minute to update status
                setTimeout(() => {
                    validateGuard();
                }, 60000);
            } else {
                // After check-out, disable both buttons
                document.getElementById("checkInBtn").disabled = true;
                document.getElementById("checkOutBtn").disabled = true;
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

    // Reset buttons to default state (enabled but will be properly set after validation)
    document.getElementById("checkInBtn").disabled = false;
    document.getElementById("checkOutBtn").disabled = false;

    // Focus back to username field
    document.getElementById("siteUsername").focus();
}

// EVENT LISTENERS
document.addEventListener('DOMContentLoaded', function () {
    console.log('DOM loaded - attaching event listeners');

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
            // Try check-in first, then check-out
            if (!document.getElementById("checkInBtn").disabled) {
                checkIn();
            } else if (!document.getElementById("checkOutBtn").disabled) {
                checkOut();
            }
        }
    });

    // Hide QR section initially, but keep buttons visible
    document.getElementById('qrSection').style.display = 'none';
    document.getElementById('currentGuardInfo').style.display = 'none';

    // Auto-validate if guard data exists in session
    const savedGuardData = sessionStorage.getItem("guardData");
    if (savedGuardData) {
        try {
            currentGuardData = JSON.parse(savedGuardData);
            const savedShiftData = sessionStorage.getItem("shiftData");
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

            // Auto-validate to get current status from database
            validateGuard();
        } catch (e) {
            console.error("Error loading saved data:", e);
            resetForm();
        }
    }

    console.log('Event listeners attached successfully');
});