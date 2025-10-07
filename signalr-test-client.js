const signalR = require("@microsoft/signalr");

// Note: You'll need to run: npm install @microsoft/signalr
// Or test with the HTML file instead

console.log("🚀 Starting SignalR Test Client...");

const connection = new signalR.HubConnectionBuilder()
    .withUrl("http://localhost:5000/hubs/stafftask")
    .build();

// Connection events
connection.start().then(function () {
    console.log("✅ Connected to SignalR Hub");
    
    // Join tenant group
    return connection.invoke("JoinTenantGroup", 1);
}).then(function() {
    console.log("✅ Joined Tenant 1 group - Ready to receive notifications!");
}).catch(function (err) {
    console.error("❌ Connection failed:", err.toString());
});

// Listen for all notification types
connection.on("TaskCreated", function (notification) {
    console.log("\n🔧 TASK CREATED NOTIFICATION:");
    console.log("═══════════════════════════════");
    console.log(`Task ID: ${notification.taskId}`);
    console.log(`Type: ${notification.taskType}`);
    console.log(`Room: ${notification.roomNumber || 'N/A'}`);
    console.log(`Priority: ${notification.priority}`);
    console.log(`Item: ${notification.requestItemName || 'N/A'}`);
    console.log(`Notes: ${notification.notes || 'N/A'}`);
    console.log(`Time: ${new Date(notification.createdAt).toLocaleString()}`);
});

connection.on("TaskUpdated", function (notification) {
    console.log("\n🔄 TASK UPDATED NOTIFICATION:");
    console.log("═══════════════════════════════");
    console.log(`Task ID: ${notification.taskId}`);
    console.log(`Status: ${notification.status}`);
    console.log(`Priority: ${notification.priority}`);
});

connection.on("TaskCompleted", function (notification) {
    console.log("\n✅ TASK COMPLETED NOTIFICATION:");
    console.log("═══════════════════════════════");
    console.log(`Task ID: ${notification.taskId}`);
    console.log(`Type: ${notification.taskType}`);
});

connection.on("EmergencyAlert", function (notification) {
    console.log("\n🚨 EMERGENCY ALERT:");
    console.log("═══════════════════════════════");
    console.log(`Incident ID: ${notification.incidentId}`);
    console.log(`Type: ${notification.emergencyType}`);
    console.log(`Severity: ${notification.severityLevel}`);
    console.log(`Location: ${notification.location || 'Unknown'}`);
    console.log(`Status: ${notification.status}`);
    console.log(`Requires Immediate Action: ${notification.requiresImmediateAction}`);
    console.log(`Requires Evacuation: ${notification.requiresEvacuation}`);
    console.log(`Description: ${notification.description}`);
});

connection.on("MaintenanceRequest", function (notification) {
    console.log("\n🔧 MAINTENANCE REQUEST:");
    console.log("═══════════════════════════════");
    console.log(`Request ID: ${notification.requestId}`);
    console.log(`Title: ${notification.title}`);
    console.log(`Category: ${notification.category}`);
    console.log(`Priority: ${notification.priority}`);
    console.log(`Location: ${notification.location || 'N/A'}`);
    console.log(`Status: ${notification.status}`);
    console.log(`Description: ${notification.description}`);
});

connection.on("Notification", function (payload) {
    console.log("\n📢 GENERIC NOTIFICATION:");
    console.log("═══════════════════════════════");
    console.log(`Type: ${payload.type}`);
    console.log(`Priority: ${payload.priority}`);
    console.log(`Message: ${payload.message}`);
    console.log("Data:", JSON.stringify(payload.data, null, 2));
});

// Handle disconnection
connection.onclose(function() {
    console.log("❌ Connection closed");
});

// Keep the process alive
process.on('SIGINT', function() {
    console.log("\n👋 Shutting down SignalR test client...");
    connection.stop().then(function() {
        process.exit(0);
    });
});

console.log("📡 Listening for notifications... (Press Ctrl+C to exit)");