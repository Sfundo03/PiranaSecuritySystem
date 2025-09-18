// script.js - Complete Guard Check-In System with Security Validation
<script src="https://cdn.jsdelivr.net/npm/qrcode/build/qrcode.min.js"></script>

// Store today's data in session
const checkinData = JSON.parse(sessionStorage.getItem("checkinData")) || [];
const today = new Date().toISOString().split("T")[0];
let isValidGuard = false;
let currentGuardData = null;
let originalValidatedGuardId = null;
let currentShiftType = null;

// Configuration
const DAY_SHIFT_CHECKIN = "06:00";
const DAY_SHIFT_CHECKOUT = "18:00";
const NIGHT_SHIFT_CHECKIN = "18:00";
const NIGHT_SHIFT_CHECKOUT = "06:00";
const GRACE_PERIOD_MINUTES = 15;

// Utility functions
function generateShortCode() {
    return Math.random().toString(36).substring(2, 8).toUpperCase();
}

function isLate(checkTime, expectedTime) {
    const [expectedHours, expectedMinutes] = expectedTime.split(':').map(Number);
    const [checkHours, checkMinutes] = checkTime.split(':').map(Number);

    const expectedTotalMinutes = expectedHours * 60 + expectedMinutes;
    const checkTotalMinutes = checkHours * 60 + checkMinutes;

    return checkTotalMinutes > (expectedTotalMinutes + GRACE_PERIOD_MINUTES);
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
    element.className = type;
}

// API CALLS
async function validateGuardByUsername(siteUsername) {
    try {
        const response = await fetch('/Guard/ValidateGuardByUsername', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: `siteUsername=${encodeURIComponent(siteUsername)}`
        });
        return response.ok ? await response.json() : { isValid: false, message: "Error validating guard" };
    } catch (error) {
        console.error("Error validating guard:", error);
        return { isValid: false, message: "Network error validating guard" };
    }
}

async function saveCheckInToDatabase(checkinRecord) {
    try {
        console.log("Saving to database:", checkinRecord);
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

// SECURITY FUNCTIONS
function resetGuardSession() {
    sessionStorage.removeItem("guardData");
    sessionStorage.removeItem("originalGuardId");
    sessionStorage.removeItem("lastToken");
    sessionStorage.removeItem("tokenTime");
    sessionStorage.removeItem("checkinData");
    currentGuardData = null;
    originalValidatedGuardId = null;
    isValidGuard = false;
    currentShiftType = null;

    document.getElementById("siteUsername").value = "";
    document.getElementById("validationMessage").textContent = "";
    document.getElementById("validationMessage").className = "";
    document.getElementById("qrSection").style.display = "none";
    document.getElementById("qrcode").innerHTML = "";
    document.getElementById("scannedCode").value = "";
    document.getElementById("statusMessage").textContent = "";
    document.getElementById("statusMessage").className = "";
}

// MAIN FUNCTIONS
async function validateGuard() {
    const siteUsername = document.getElementById("siteUsername").value.trim();
    const validationMessage = document.getElementById("validationMessage");
    const qrSection = document.getElementById("qrSection");
    const message = document.getElementById("statusMessage");

    validationMessage.className = "";
    message.className = "";

    if (!siteUsername) {
        validationMessage.textContent = "Please enter your site username.";
        validationMessage.className = "warning";
        return;
    }

    // SECURITY: Prevent guard switching
    if (originalValidatedGuardId && currentGuardData && currentGuardData.guardId !== originalValidatedGuardId) {
        validationMessage.textContent = "Security violation: You cannot check in/out for another guard.";
        validationMessage.className = "error";
        qrSection.style.display = "none";
        return;
    }

    try {
        const result = await validateGuardByUsername(siteUsername);

        if (result.isValid) {
            // SECURITY: Store original guard ID
            if (!originalValidatedGuardId) {
                originalValidatedGuardId = result.guardData.guardId;
                sessionStorage.setItem("originalGuardId", originalValidatedGuardId.toString());
            }

            // SECURITY: Prevent different guard validation
            if (originalValidatedGuardId && result.guardData.guardId !== originalValidatedGuardId) {
                validationMessage.textContent = "Security violation: You cannot check in/out for another guard.";
                validationMessage.className = "error";
                qrSection.style.display = "none";
                return;
            }

            currentGuardData = result.guardData;
            currentShiftType = determineShiftType();

            // Get today's check-ins
            const todayCheckinsResult = await getTodayGuardCheckIns(currentGuardData.guardId);
            const todayCheckins = todayCheckinsResult.success ? todayCheckinsResult.checkins : [];

            const hasCheckedIn = todayCheckins.some(e => e.status === "Present" || e.status === "Late Arrival");
            const hasCheckedOut = todayCheckins.some(e => e.status === "Checked Out" || e.status === "Late Departure");

            validationMessage.textContent = `Welcome, ${currentGuardData.fullName}!`;
            validationMessage.className = "success";

            // Update guard info display
            document.getElementById("currentGuardInfo").textContent =
                `${currentGuardData.fullName} (${currentShiftType} Shift)`;

            if (hasCheckedOut) {
                validationMessage.textContent += " You have already completed your shift today.";
                qrSection.style.display = "none";
            } else if (hasCheckedIn) {
                validationMessage.textContent += " You are currently checked in.";
                document.getElementById("checkInBtn").style.display = "none";
                document.getElementById("checkOutBtn").style.display = "block";
                qrSection.style.display = "block";
            } else {
                validationMessage.textContent += " You can check in now.";
                document.getElementById("checkInBtn").style.display = "block";
                document.getElementById("checkOutBtn").style.display = "none";
                qrSection.style.display = "block";
            }

            isValidGuard = true;
            sessionStorage.setItem("guardData", JSON.stringify(currentGuardData));
            message.textContent = "";
        } else {
            validationMessage.textContent = result.message || "Site username not recognized.";
            validationMessage.className = "error";
            qrSection.style.display = "none";
        }
    } catch (error) {
        console.error("Error:", error);
        validationMessage.textContent = "Network error. Please try again.";
        validationMessage.className = "error";
        qrSection.style.display = "none";
    }
}

function generateQRCode() {
    if (!isValidGuard || !currentGuardData) {
        showMessage("Please validate your identity first.", "warning");
        return;
    }

    const qrcodeDiv = document.getElementById("qrcode");
    const message = document.getElementById("statusMessage");
    message.className = "";

    // Check if already completed shift
    const todayCheckins = checkinData.filter(entry =>
        entry.guardId === currentGuardData.guardId && entry.date === today
    );

    const hasCheckedOut = todayCheckins.some(entry => entry.status === "Checked Out" || entry.status === "Late Departure");
    if (hasCheckedOut) {
        showMessage("You have already completed your shift today.", "error");
        return;
    }

    const token = generateShortCode();
    qrcodeDiv.innerHTML = "";

    const canvas = document.createElement("canvas");
    QRCode.toCanvas(canvas, token, function (error) {
        if (error) {
            showMessage("Error generating QR code.", "error");
            return;
        }
        qrcodeDiv.appendChild(canvas);
    });

    sessionStorage.setItem("lastToken", token);
    sessionStorage.setItem("tokenTime", new Date().getTime().toString());
    showMessage("QR Code generated. Please scan and enter the code.", "success");
}

async function checkIn() {
    const expectedTime = getExpectedTime(currentShiftType, 'checkin');
    await verifyScan("Present", expectedTime);
}

async function checkOut() {
    const expectedTime = getExpectedTime(currentShiftType, 'checkout');
    await verifyScan("Checked Out", expectedTime);
}

async function verifyScan(statusType, expectedTime) {
    const scanned = document.getElementById("scannedCode").value.trim().toUpperCase();
    const message = document.getElementById("statusMessage");
    message.className = "";

    const exactToken = sessionStorage.getItem("lastToken");
    const tokenTime = parseInt(sessionStorage.getItem("tokenTime") || "0");
    const tokenAge = (new Date().getTime() - tokenTime) / 1000 / 60;

    if (tokenAge > 5) {
        showMessage("QR code expired. Please generate a new one.", "error");
        sessionStorage.removeItem("lastToken");
        sessionStorage.removeItem("tokenTime");
        return;
    }

    if (!scanned || !exactToken || !currentGuardData) {
        showMessage("Please generate and scan a QR code first.", "warning");
        return;
    }

    if (scanned !== exactToken) {
        showMessage("Invalid QR code.", "error");
        return;
    }

    const currentTime = getCurrentTimeString();
    const isLateArrival = statusType === "Present" && isLate(currentTime, expectedTime);
    const isLateDeparture = statusType === "Checked Out" && isLate(currentTime, expectedTime);

    let statusWithTiming = statusType;
    if (isLateArrival) statusWithTiming = "Late Arrival";
    if (isLateDeparture) statusWithTiming = "Late Departure";

    const checkinRecord = {
        guardId: currentGuardData.guardId,
        siteUsername: currentGuardData.siteUsername,
        name: currentGuardData.fullName,
        status: statusWithTiming,
        expectedTime: expectedTime,
        actualTime: currentTime,
        isLate: isLateArrival || isLateDeparture,
        token: exactToken
    };

    try {
        const result = await saveCheckInToDatabase(checkinRecord);

        if (result.success) {
            // For local storage
            const localRecord = { ...checkinRecord, date: today, time: currentTime };
            checkinData.push(localRecord);
            sessionStorage.setItem("checkinData", JSON.stringify(checkinData));

            let statusMessage = `${statusType} recorded successfully at ${currentTime}!`;
            if (isLateArrival || isLateDeparture) {
                statusMessage += ` (${statusWithTiming})`;
            }

            showMessage(statusMessage, isLateArrival || isLateDeparture ? "warning" : "success");
            document.getElementById("scannedCode").value = "";

            if (statusType === "Present") {
                document.getElementById("checkInBtn").style.display = "none";
                document.getElementById("checkOutBtn").style.display = "block";
            } else {
                document.getElementById("qrSection").style.display = "none";
                isValidGuard = false;
            }
        } else {
            showMessage(result.message || "Error saving to database.", "error");
        }
    } catch (error) {
        showMessage("Error saving to database. Please try again.", "error");
    }

    sessionStorage.removeItem("lastToken");
    sessionStorage.removeItem("tokenTime");
}

// EVENT LISTENERS
document.addEventListener('DOMContentLoaded', function () {
    // Load existing data
    const savedGuardData = sessionStorage.getItem("guardData");
    if (savedGuardData) {
        currentGuardData = JSON.parse(savedGuardData);
    }

    const savedOriginalGuardId = sessionStorage.getItem("originalGuardId");
    if (savedOriginalGuardId) {
        originalValidatedGuardId = parseInt(savedOriginalGuardId);
    }

    // Clear username field
    document.getElementById("siteUsername").value = "";

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
});