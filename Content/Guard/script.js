// script.js - Fixed Guard Check-In System

// Store today's data in session
let checkinData = JSON.parse(sessionStorage.getItem("checkinData")) || [];
const today = new Date().toISOString().split("T")[0];
let isValidGuard = false;
let currentGuardData = null;
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
    element.className = type;
    element.style.display = "block";
}

function showValidationMessage(text, type) {
    const element = document.getElementById("validationMessage");
    element.textContent = text;
    element.className = type;
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

// MAIN FUNCTIONS
async function validateGuard() {
    console.log('validateGuard function called');

    const siteUsername = document.getElementById("siteUsername").value.trim();
    const validationMessage = document.getElementById("validationMessage");
    const qrSection = document.getElementById("qrSection");

    // Reset messages
    validationMessage.textContent = "";
    validationMessage.className = "";
    validationMessage.style.display = "none";
    showMessage("", "");

    if (!siteUsername) {
        showValidationMessage("Please enter your site username.", "error");
        return;
    }

    try {
        console.log('Calling validateGuardByUsername API');
        const result = await validateGuardByUsername(siteUsername);
        console.log('API result:', result);

        if (result.isValid) {
            currentGuardData = result.guardData;
            currentShiftType = determineShiftType();

            // Get today's check-ins
            const todayCheckinsResult = await getTodayGuardCheckIns(currentGuardData.guardId);
            const todayCheckins = todayCheckinsResult.success ? todayCheckinsResult.checkins : [];

            const hasCheckedIn = todayCheckins.some(e => e.status === "Present" || e.status === "Late Arrival");
            const hasCheckedOut = todayCheckins.some(e => e.status === "Checked Out" || e.status === "Late Departure");

            showValidationMessage(`Welcome, ${currentGuardData.fullName}! (${currentShiftType} Shift)`, "success");

            // Update guard info display
            document.getElementById("currentGuardInfo").textContent =
                `${currentGuardData.fullName} - ${currentShiftType} Shift`;

            if (hasCheckedOut) {
                showValidationMessage(" You have already completed your shift today.", "success");
                qrSection.style.display = "none";
                isValidGuard = false;
            } else if (hasCheckedIn) {
                // Check if it's allowed to checkout yet
                const canCheckout = isTimeAllowedForCheckout();
                if (canCheckout) {
                    showValidationMessage(" You can now check out.", "success");
                    document.getElementById("checkInBtn").style.display = "none";
                    document.getElementById("checkOutBtn").style.display = "block";
                    qrSection.style.display = "block";
                } else {
                    const nextActionTime = currentShiftType === 'Day' ? '6:00 PM' : '6:00 AM';
                    showValidationMessage(` You are checked in. Checkout will be available after ${nextActionTime}.`, "info");
                    document.getElementById("checkInBtn").style.display = "none";
                    document.getElementById("checkOutBtn").style.display = "none";
                    qrSection.style.display = "none";
                }
            } else {
                showValidationMessage(" You can check in now.", "success");
                document.getElementById("checkInBtn").style.display = "block";
                document.getElementById("checkOutBtn").style.display = "none";
                qrSection.style.display = "block";
            }

            isValidGuard = true;
            sessionStorage.setItem("guardData", JSON.stringify(currentGuardData));
        } else {
            showValidationMessage(result.message || "Site username not recognized.", "error");
            qrSection.style.display = "none";
            isValidGuard = false;
        }
    } catch (error) {
        console.error("Error:", error);
        showValidationMessage("Network error. Please try again.", "error");
        qrSection.style.display = "none";
        isValidGuard = false;
    }
}

function generateQRCode() {
    if (!isValidGuard || !currentGuardData) {
        showMessage("Please validate your identity first.", "error");
        return;
    }

    const qrcodeDiv = document.getElementById("qrcode");

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

    // Generate QR code
    QRCode.toCanvas(document.getElementById("qrcode"), token, function (error) {
        if (error) {
            showMessage("Error generating QR code.", "error");
            return;
        }
    });

    sessionStorage.setItem("lastToken", token);
    sessionStorage.setItem("tokenTime", new Date().getTime().toString());
    showMessage("QR Code generated. Please scan and enter the code.", "success");
}

async function checkIn() {
    if (!isTimeAllowedForCheckout()) {
        const expectedTime = getExpectedTime(currentShiftType, 'checkin');
        await verifyScan("Present", expectedTime);
    } else {
        showMessage("Cannot check in during checkout hours.", "error");
    }
}

async function checkOut() {
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
        showMessage("Please generate and scan a QR code first.", "error");
        return;
    }

    if (scanned !== exactToken) {
        showMessage("Invalid QR code.", "error");
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
            if (isLate) {
                statusMessage += ` (${statusWithTiming})`;
            }

            showMessage(statusMessage, isLate ? "warning" : "success");
            document.getElementById("scannedCode").value = "";

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

    sessionStorage.removeItem("lastToken");
    sessionStorage.removeItem("tokenTime");
}

function resetForm() {
    sessionStorage.removeItem("guardData");
    sessionStorage.removeItem("lastToken");
    sessionStorage.removeItem("tokenTime");
    currentGuardData = null;
    isValidGuard = false;

    document.getElementById("siteUsername").value = "";
    document.getElementById("validationMessage").textContent = "";
    document.getElementById("validationMessage").className = "";
    document.getElementById("validationMessage").style.display = "none";
    document.getElementById("qrSection").style.display = "none";
    document.getElementById("qrcode").innerHTML = "";
    document.getElementById("scannedCode").value = "";
    document.getElementById("statusMessage").textContent = "";
    document.getElementById("statusMessage").className = "";
    document.getElementById("statusMessage").style.display = "none";
    document.getElementById("currentGuardInfo").textContent = "";
}

// EVENT LISTENERS
document.addEventListener('DOMContentLoaded', function () {
    console.log('DOM loaded - attaching event listeners');

    // Load existing data
    const savedGuardData = sessionStorage.getItem("guardData");
    if (savedGuardData) {
        currentGuardData = JSON.parse(savedGuardData);
        isValidGuard = true;
        document.getElementById("currentGuardInfo").textContent =
            `${currentGuardData.fullName} - ${determineShiftType()} Shift`;
    }

    // Clear username field
    document.getElementById("siteUsername").value = "";

    // Add click event listener to validate button
    document.querySelector('button[onclick="validateGuard()"]').addEventListener('click', validateGuard);

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

    console.log('Event listeners attached successfully');
});