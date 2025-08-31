// script.js

const checkinData = JSON.parse(sessionStorage.getItem("checkinData")) || [];
const today = new Date().toISOString().split("T")[0];
let isValidGuard = false;

function generateShortCode() {
    return Math.random().toString(36).substring(2, 8).toUpperCase();
}

async function validateGuard() {
    const name = document.getElementById("guardName").value.trim();
    const validationMessage = document.getElementById("validationMessage");
    const qrSection = document.getElementById("qrSection");
    const message = document.getElementById("statusMessage");

    if (!name) {
        validationMessage.textContent = "Please enter your first name.";
        validationMessage.style.color = "orange";
        return;
    }

    try {
        // Call MVC endpoint to validate the guard
        const response = await fetch('/Guard/ValidateGuard', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
            },
            body: `firstName=${encodeURIComponent(name)}`
        });

        if (response.ok) {
            const result = await response.json();
            if (result.isValid) {
                validationMessage.textContent = "Guard validated successfully!";
                validationMessage.style.color = "green";
                qrSection.style.display = "block";
                isValidGuard = true;
                sessionStorage.setItem("guardName", name);
                sessionStorage.setItem("guardId", result.guardId);

                // Clear any previous status messages
                message.textContent = "";
            } else {
                validationMessage.textContent = "Guard name not recognized. Please try again.";
                validationMessage.style.color = "red";
                qrSection.style.display = "none";
                isValidGuard = false;
            }
        } else {
            validationMessage.textContent = "Error validating guard. Please try again.";
            validationMessage.style.color = "red";
            qrSection.style.display = "none";
            isValidGuard = false;
        }
    } catch (error) {
        console.error("Error:", error);
        validationMessage.textContent = "Network error. Please try again.";
        validationMessage.style.color = "red";
        qrSection.style.display = "none";
        isValidGuard = false;
    }
}

function generateQRCode() {
    if (!isValidGuard) {
        const message = document.getElementById("statusMessage");
        message.textContent = "Please validate your identity first.";
        message.style.color = "orange";
        return;
    }

    const name = sessionStorage.getItem("guardName");
    const qrcodeDiv = document.getElementById("qrcode");
    const message = document.getElementById("statusMessage");

    // ✅ Check if guard already checked in/out today
    const alreadyExists = checkinData.some(entry => entry.name === name && entry.date === today);
    if (alreadyExists) {
        message.textContent = "You have already scanned today. Cannot generate again.";
        message.style.color = "red";
        return;
    }

    const token = generateShortCode();

    // Clear existing QR codes
    qrcodeDiv.innerHTML = "";

    // Generate new QR code
    const canvas = document.createElement("canvas");
    QRCode.toCanvas(canvas, token, function (error) {
        if (error) {
            console.error("QR Code generation error:", error);
            message.textContent = "Error generating QR code. Please try again.";
            message.style.color = "red";
        }
    });
    qrcodeDiv.appendChild(canvas);

    // Save token temporarily
    sessionStorage.setItem("lastToken", token);  // store exact short code

    message.textContent = "QR Code generated. Please scan and enter the code.";
    message.style.color = "green";
}

async function checkIn() {
    await verifyScan("Present");
}

async function checkOut() {
    await verifyScan("Checked Out");
}

async function verifyScan(statusType) {
    const scanned = document.getElementById("scannedCode").value.trim().toUpperCase();
    const message = document.getElementById("statusMessage");

    const exactToken = sessionStorage.getItem("lastToken");
    const name = sessionStorage.getItem("guardName");
    const guardId = sessionStorage.getItem("guardId");

    if (!scanned || !exactToken || !name || !guardId) {
        message.textContent = "Please generate and scan a QR code first.";
        message.style.color = "orange";
        return;
    }

    // ✅ Check if scanned matches exact token
    if (scanned !== exactToken) {
        message.textContent = "Invalid or expired QR code.";
        message.style.color = "red";
        return;
    }

    const time = new Date().toLocaleTimeString();
    const date = new Date().toISOString().split("T")[0];

    // Save to session storage
    checkinData.push({ name, time, status: statusType, date: today });
    sessionStorage.setItem("checkinData", JSON.stringify(checkinData));

    try {
        // Save to database using MVC endpoint
        const response = await fetch('/Guard/SaveCheckIn', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
            },
            body: `guardId=${guardId}&status=${encodeURIComponent(statusType)}`
        });

        if (response.ok) {
            const result = await response.json();
            if (result.success) {
                message.textContent = `${statusType} recorded successfully at ${time}!`;
                message.style.color = "green";

                // Clear the input fields
                document.getElementById("scannedCode").value = "";

                // Hide QR section until next validation
                document.getElementById("qrSection").style.display = "none";
                isValidGuard = false;
            } else {
                message.textContent = "Scan recorded locally but failed to save to database.";
                message.style.color = "orange";
            }
        } else {
            message.textContent = "Scan recorded locally but failed to save to database.";
            message.style.color = "orange";
        }
    } catch (error) {
        console.error("Error saving to database:", error);
        message.textContent = "Scan recorded locally but failed to save to database.";
        message.style.color = "orange";
    }

    // ✅ Prevent same guard from scanning again
    sessionStorage.removeItem("lastToken");
}

// Add event listeners for Enter key
document.addEventListener('DOMContentLoaded', function () {
    // Allow pressing Enter in the name field to validate
    document.getElementById("guardName").addEventListener("keypress", function (event) {
        if (event.key === "Enter") {
            event.preventDefault();
            validateGuard();
        }
    });

    // Allow pressing Enter in the scanned code field to check in
    document.getElementById("scannedCode").addEventListener("keypress", function (event) {
        if (event.key === "Enter") {
            event.preventDefault();
            checkIn();
        }
    });
});