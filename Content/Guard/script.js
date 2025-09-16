< !DOCTYPE html >
    <html lang="en">
        <head>
            <meta charset="UTF-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                    <title>Guard Check-in System</title>
                    <script src="https://cdn.jsdelivr.net/npm/qrcode@1.5.1/build/qrcode.min.js"></script>
                    <style>
                        * {
                            box - sizing: border-box;
                        margin: 0;
                        padding: 0;
                        font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
        }

                        body {
                            background: linear-gradient(135deg, #1a2a6c, #b21f1f, #fdbb2d);
                        min-height: 100vh;
                        display: flex;
                        justify-content: center;
                        align-items: center;
                        padding: 20px;
        }

                        .container {
                            background - color: rgba(255, 255, 255, 0.9);
                        border-radius: 15px;
                        box-shadow: 0 10px 30px rgba(0, 0, 0, 0.3);
                        width: 100%;
                        max-width: 500px;
                        padding: 30px;
                        text-align: center;
        }

                        h1 {
                            color: #2c3e50;
                        margin-bottom: 20px;
                        font-size: 28px;
        }

                        .date-display {
                            color: #7f8c8d;
                        margin-bottom: 25px;
                        font-size: 16px;
        }

                        .input-group {
                            margin - bottom: 20px;
                        text-align: left;
        }

                        label {
                            display: block;
                        margin-bottom: 8px;
                        font-weight: 600;
                        color: #2c3e50;
        }

                        input[type="text"] {
                            width: 100%;
                        padding: 12px 15px;
                        border: 2px solid #ddd;
                        border-radius: 8px;
                        font-size: 16px;
                        transition: border-color 0.3s;
        }

                        input[type="text"]:focus {
                            border - color: #3498db;
                        outline: none;
        }

                        button {
                            background - color: #3498db;
                        color: white;
                        border: none;
                        padding: 12px 20px;
                        border-radius: 8px;
                        cursor: pointer;
                        font-size: 16px;
                        font-weight: 600;
                        transition: background-color 0.3s;
                        margin: 5px;
        }

                        button:hover {
                            background - color: #2980b9;
        }

                        #checkInBtn {
                            background - color: #2ecc71;
        }

                        #checkInBtn:hover {
                            background - color: #27ae60;
        }

                        #checkOutBtn {
                            background - color: #e74c3c;
        }

                        #checkOutBtn:hover {
                            background - color: #c0392b;
        }

                        .message {
                            padding: 12px;
                        border-radius: 8px;
                        margin: 15px 0;
                        font-weight: 500;
        }

                        .success {
                            background - color: #d4edda;
                        color: #155724;
                        border: 1px solid #c3e6cb;
        }

                        .error {
                            background - color: #f8d7da;
                        color: #721c24;
                        border: 1px solid #f5c6cb;
        }

                        .warning {
                            background - color: #fff3cd;
                        color: #856404;
                        border: 1px solid #ffeaa7;
        }

                        .info {
                            background - color: #d1ecf1;
                        color: #0c5460;
                        border: 1px solid #bee5eb;
        }

                        #qrSection {
                            margin: 20px 0;
                        padding: 20px;
                        border: 2px dashed #3498db;
                        border-radius: 10px;
                        background-color: #f8f9fa;
        }

                        #qrcode {
                            margin: 15px 0;
                        display: flex;
                        justify-content: center;
        }

                        .instructions {
                            background - color: #e3f2fd;
                        padding: 15px;
                        border-radius: 8px;
                        margin: 20px 0;
                        text-align: left;
                        font-size: 14px;
        }

                        .instructions h3 {
                            margin - bottom: 10px;
                        color: #1565c0;
        }

                        .instructions ol {
                            padding - left: 20px;
        }

                        .instructions li {
                            margin - bottom: 8px;
        }

                        .footer {
                            margin - top: 25px;
                        color: #7f8c8d;
                        font-size: 14px;
        }
                    </style>
                </head>
                <body>
                    <div class="container">
                        <h1>Guard Check-in System</h1>
                        <div class="date-display" id="currentDate">Loading date...</div>

                        <div class="input-group">
                            <label for="guardName">Enter Your First Name:</label>
                            <input type="text" id="guardName" placeholder="e.g. John">
                        </div>

                        <button id="validateBtn" onclick="validateGuard()">Validate Identity</button>

                        <div id="validationMessage" class="message"></div>

                        <div id="qrSection" style="display: none;">
                            <h2>QR Code Generator</h2>
                            <button onclick="generateQRCode()">Generate QR Code</button>
                            <div id="qrcode"></div>

                            <div class="input-group">
                                <label for="scannedCode">Enter Scanned Code:</label>
                                <input type="text" id="scannedCode" placeholder="Enter code from QR scan">
                            </div>

                            <button id="checkInBtn" onclick="checkIn()">Check In</button>
                            <button id="checkOutBtn" onclick="checkOut()">Check Out</button>
                        </div>

                        <div id="statusMessage" class="message"></div>

                        <div class="instructions">
                            <h3>How to Use:</h3>
                            <ol>
                                <li>Enter your first name and click "Validate Identity"</li>
                                <li>If validated successfully, click "Generate QR Code"</li>
                                <li>Scan the QR code with your device</li>
                                <li>Enter the code in the input field</li>
                                <li>Click "Check In" or "Check Out" as needed</li>
                            </ol>
                        </div>

                        <div class="footer">
                            <p>Pirana Security System &copy; 2023</p>
                        </div>
                    </div>

                    <script>
        // Initialize session storage for checkin data if not exists
                        const checkinData = JSON.parse(sessionStorage.getItem("checkinData")) || [];
                        const today = new Date().toISOString().split("T")[0];
                        let isValidGuard = false;
                        let currentGuardId = null;

                        // Display current date
                        document.getElementById("currentDate").textContent = "Today: " + new Date().toLocaleDateString('en-US', {
                            weekday: 'long',
                        year: 'numeric',
                        month: 'long',
                        day: 'numeric' 
        });

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
                        validationMessage.className = "message warning";
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
                        validationMessage.className = "message success";
                        qrSection.style.display = "block";
                        isValidGuard = true;
                        currentGuardId = result.guardId;

                        sessionStorage.setItem("guardName", name);
                        sessionStorage.setItem("guardId", result.guardId);

                        // Clear any previous status messages
                        message.textContent = "";
                        message.className = "message";
                    } else {
                            validationMessage.textContent = "Guard name not recognized. Please try again.";
                        validationMessage.className = "message error";
                        qrSection.style.display = "none";
                        isValidGuard = false;
                        currentGuardId = null;
                    }
                } else {
                            validationMessage.textContent = "Error validating guard. Please try again.";
                        validationMessage.className = "message error";
                        qrSection.style.display = "none";
                        isValidGuard = false;
                        currentGuardId = null;
                }
            } catch (error) {
                            console.error("Error:", error);
                        validationMessage.textContent = "Network error. Please try again.";
                        validationMessage.className = "message error";
                        qrSection.style.display = "none";
                        isValidGuard = false;
                        currentGuardId = null;
            }
        }

                        function generateQRCode() {
            if (!isValidGuard || !currentGuardId) {
                const message = document.getElementById("statusMessage");
                        message.textContent = "Please validate your identity first.";
                        message.className = "message warning";
                        return;
            }

                        const name = sessionStorage.getItem("guardName");
                        const qrcodeDiv = document.getElementById("qrcode");
                        const message = document.getElementById("statusMessage");

            // Check if guard already checked in/out today
            const alreadyExists = checkinData.some(entry => entry.name === name && entry.date === today);
                        if (alreadyExists) {
                            message.textContent = "You have already scanned today. Cannot generate again.";
                        message.className = "message error";
                        return;
            }

                        // Generate token with guardId embedded
                        const token = generateShortCode() + "-" + currentGuardId;

                        // Clear existing QR codes
                        qrcodeDiv.innerHTML = "";

                        // Generate new QR code
                        const canvas = document.createElement("canvas");
                        QRCode.toCanvas(canvas, token, function (error) {
                if (error) {
                            console.error("QR Code generation error:", error);
                        message.textContent = "Error generating QR code. Please try again.";
                        message.className = "message error";
                }
            });
                        qrcodeDiv.appendChild(canvas);

                        // Save token temporarily with guardId
                        sessionStorage.setItem("lastToken", token);

                        message.textContent = "QR Code generated. Please scan and enter the code.";
                        message.className = "message success";
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

                        const storedToken = sessionStorage.getItem("lastToken");
                        const name = sessionStorage.getItem("guardName");
                        const guardId = sessionStorage.getItem("guardId");

                        if (!scanned || !storedToken || !name || !guardId) {
                            message.textContent = "Please generate and scan a QR code first.";
                        message.className = "message warning";
                        return;
            }

                        // Extract guardId from token (format: CODE-GUARDID)
                        const tokenParts = storedToken.split('-');
            const tokenGuardId = tokenParts.length > 1 ? tokenParts[1] : null;

                        // Check if the scanned code matches and if the guardId matches
                        if (scanned !== storedToken || tokenGuardId !== guardId) {
                            message.textContent = "Invalid or expired QR code. This code is not authorized for your account.";
                        message.className = "message error";
                        return;
            }

                        const time = new Date().toLocaleTimeString();
                        const date = new Date().toISOString().split("T")[0];

                        // Save to session storage
                        checkinData.push({name, time, status: statusType, date: today });
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
                        message.className = "message success";

                        // Clear the input fields
                        document.getElementById("scannedCode").value = "";

                        // Hide QR section until next validation
                        document.getElementById("qrSection").style.display = "none";
                        isValidGuard = false;
                        currentGuardId = null;
                    } else {
                            message.textContent = "Scan recorded locally but failed to save to database.";
                        message.className = "message warning";
                    }
                } else {
                            message.textContent = "Scan recorded locally but failed to save to database.";
                        message.className = "message warning";
                }
            } catch (error) {
                            console.error("Error saving to database:", error);
                        message.textContent = "Scan recorded locally but failed to save to database.";
                        message.className = "message warning";
            }

                        // Prevent same guard from scanning again
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
                    </script>
                </body>
            </html>