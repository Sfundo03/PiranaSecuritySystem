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

function isTimeAllowedForCheckout() {
    const now = new Date();
    const hours = now.getHours();
    const currentShift = determineShiftType();

    if (currentShift === 'Day') {
        return hours >= 18;
    } else {
        return hours >= 6 && hours < 18;
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

// API CALLS
async function validateGuardByUsername(siteUsername) {
    try {
        console.log('Validating guard:', siteUsername);

        const response = await fetch('/Guard/ValidateGuardByUsername', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded'
            },
            body: `siteUsername=${encodeURIComponent(siteUsername)}`
        });

        console.log('Response status:', response.status);

        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const data = await response.json();
        console.log('Response data:', data);
        return data;
    } catch (error) {
        console.error("Error validating guard:", error);
        return { isValid: false, message: "Network error validating guard" };
    }
}

async function getTodaysShift(guardId) {
    try {
        const response = await fetch(`/Guard/GetTodaysShift?guardId=${guardId}&date=${today}`);
        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }
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
                'Content-Type': 'application/json'
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
    const message = document.getElementById("statusMessage");

    // Clear previous QR code and input
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

    try {
        // Clear the QR code div
        qrcodeDiv.innerHTML = "";

        // Create a canvas element
        const canvas = document.createElement("canvas");

        // Generate QR code
        QRCode.toCanvas(canvas, token, {
            width: 200,
            margin: 1,
            colorDark: "#000000",
            colorLight: "#ffffff",
            correctLevel: QRCode.CorrectLevel.H
        }, function (error) {
            if (error) {
                console.error("QR generation failed:", error);
                message.textContent = "QR generation failed. Please try again.";
                message.className = "error";
                generateSimpleQRCode(token, qrcodeDiv);
                return;
            }

            // Add canvas to the div
            qrcodeDiv.appendChild(canvas);

            // Add clear button
            const clearBtn = document.createElement("button");
            clearBtn.textContent = "Clear QR Code";
            clearBtn.onclick = clearQRCode;
            clearBtn.style.marginTop = "10px";
            clearBtn.style.padding = "5px 10px";
            clearBtn.style.backgroundColor = "#6c757d";
            clearBtn.style.color = "white";
            clearBtn.style.border = "none";
            clearBtn.style.borderRadius = "3px";
            clearBtn.style.cursor = "pointer";
            qrcodeDiv.appendChild(clearBtn);

            message.textContent = "QR Code generated. Please scan and enter the code: " + token;
            message.className = "success";

            console.log("QR Code generated successfully with token:", token);
        });

    } catch (error) {
        console.error("QR Code generation error:", error);
        generateSimpleQRCode(token, qrcodeDiv);
    }

    sessionStorage.setItem("lastToken", token);
    sessionStorage.setItem("tokenTime", new Date().getTime().toString());
    scannedCodeInput.focus();
}

function clearQRCode() {
    const qrcodeDiv = document.getElementById("qrcode");
    qrcodeDiv.innerHTML = "";
    document.getElementById("statusMessage").textContent = "";
    document.getElementById("statusMessage").className = "";
    sessionStorage.removeItem("lastToken");
    sessionStorage.removeItem("tokenTime");
}

function generateSimpleQRCode(token, qrcodeDiv) {
    const container = document.createElement('div');
    container.style.textAlign = 'center';
    container.style.padding = '20px';
    container.style.border = '2px dashed #ccc';
    container.style.borderRadius = '10px';
    container.style.background = '#f9f9f9';

    const codeDisplay = document.createElement('div');
    codeDisplay.style.fontSize = '24px';
    codeDisplay.style.fontWeight = 'bold';
    codeDisplay.style.letterSpacing = '3px';
    codeDisplay.style.margin = '10px 0';
    codeDisplay.style.padding = '10px';
    codeDisplay.style.background = '#000';
    codeDisplay.style.color = '#fff';
    codeDisplay.style.borderRadius = '5px';
    codeDisplay.textContent = token;

    const instruction = document.createElement('p');
    instruction.style.color = '#666';
    instruction.textContent = 'Enter this code manually:';

    container.appendChild(instruction);
    container.appendChild(codeDisplay);
    qrcodeDiv.appendChild(container);

    console.log("Simple QR Code representation generated with token:", token);
    showMessage("Code generated. Please enter this code: " + token, "success");
}

// MAIN FUNCTIONS
async function validateGuard() {
    console.log('validateGuard function called');

    const siteUsername = document.getElementById("siteUsername").value.trim();
    const validationMessage = document.getElementById("validationMessage");
    const qrSection = document.getElementById("qrSection");

    // Reset messages and UI
    validationMessage.textContent = "";
    validationMessage.className = "";
    validationMessage.style.display = "none";
    showMessage("", "");
    qrSection.style.display = "none";
    document.getElementById("checkInBtn").style.display = "none";
    document.getElementById("checkOutBtn").style.display = "none";
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

            // Get today's check-ins
            const todayCheckinsResult = await getTodayGuardCheckIns(currentGuardData.guardId);
            const todayCheckins = todayCheckinsResult.success ? todayCheckinsResult.checkins : [];

            const hasCheckedIn = todayCheckins.some(e => e.status === "Present" || e.status === "Late Arrival");
            const hasCheckedOut = todayCheckins.some(e => e.status === "Checked Out" || e.status === "Late Departure");

            // Update guard info display
            const shiftInfo = currentShiftData ?
                `${currentShiftType} Shift at ${currentShiftData.location || "Main Gate"}` :
                `${currentShiftType} Shift`;

            document.getElementById("currentGuardInfo").textContent =
                `${currentGuardData.fullName} - ${shiftInfo}`;
            document.getElementById("currentGuardInfo").style.display = "block";

            if (hasCheckedOut) {
                showValidationMessage(`Welcome, ${currentGuardData.fullName}! You have already completed your shift today.`, "success");
                qrSection.style.display = "none";
                isValidGuard = false;
            } else if (hasCheckedIn) {
                // Check if it's allowed to checkout yet
                const canCheckout = isTimeAllowedForCheckout();
                if (canCheckout) {
                    showValidationMessage(`Welcome, ${currentGuardData.fullName}! You can now check out.`, "success");
                    document.getElementById("checkInBtn").style.display = "none";
                    document.getElementById("checkOutBtn").style.display = "block";
                    qrSection.style.display = "block";
                } else {
                    const nextActionTime = currentShiftType === 'Day' ? '6:00 PM' : '6:00 AM';
                    showValidationMessage(`Welcome, ${currentGuardData.fullName}! You are checked in. Checkout will be available after ${nextActionTime}.`, "info");
                    document.getElementById("checkInBtn").style.display = "none";
                    document.getElementById("checkOutBtn").style.display = "none";
                    qrSection.style.display = "none";
                }
                isValidGuard = true;
            } else {
                showValidationMessage(`Welcome, ${currentGuardData.fullName}! You can check in now.`, "success");
                document.getElementById("checkInBtn").style.display = "block";
                document.getElementById("checkOutBtn").style.display = "none";
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
            isValidGuard = false;
        }
    } catch (error) {
        console.error("Error:", error);
        validateBtn.textContent = originalText;
        validateBtn.disabled = false;
        showValidationMessage("Network error. Please try again.", "error");
        qrSection.style.display = "none";
        document.getElementById("currentGuardInfo").style.display = "none";
        isValidGuard = false;
    }
}

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

    if (!isTimeAllowedForCheckout()) {
        const expectedTime = getExpectedTime(currentShiftType, 'checkin');
        await verifyScan("Present", expectedTime);
    } else {
        showMessage("Cannot check in during checkout hours.", "error");
    }
}

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

    if (isTimeAllowedForCheckout()) {
        const expectedTime = getExpectedTime(currentShiftType, 'checkout');
        await verifyScan("Checked Out", expectedTime);
    } else {
        const nextActionTime = currentShiftType === 'Day' ? '6:00 PM' : '6:00 AM';
        showMessage(`Checkout is only allowed after ${nextActionTime}.`, "error");
    }
}

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
        name: currentGuardData.fullName,
        status: statusWithTiming,
        expectedTime: expectedTime,
        actualTime: currentTime,
        isLate: isLate,
        token: exactToken,
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
                date: today,
                time: currentTime,
                timestamp: new Date().toISOString()
            };
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
                document.getElementById("checkInBtn").style.display = "none";
                if (isTimeAllowedForCheckout()) {
                    document.getElementById("checkOutBtn").style.display = "block";
                    showMessage("You are checked in. You can now check out.", "success");
                } else {
                    document.getElementById("checkOutBtn").style.display = "none";
                    const nextActionTime = currentShiftType === 'Day' ? '6:00 PM' : '6:00 AM';
                    showMessage(`You are checked in. Checkout will be available after ${nextActionTime}.`, "info");
                }
            } else {
                document.getElementById("qrSection").style.display = "none";
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