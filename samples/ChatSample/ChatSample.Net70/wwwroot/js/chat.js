"use strict";

var connection = new signalR.HubConnectionBuilder().withUrl("/chat").build();

//Disable the send button until connection is established.
document.getElementById("getButton").disabled = true;
document.getElementById("sendButton").disabled = true;

var messageCallback = function (message) {
    var li = document.createElement("li");
    document.getElementById("messagesList").appendChild(li);
    // We can assign user-supplied strings to an element's textContent because it
    // is not interpreted as markup. If you're assigning in any other way, you 
    // should be aware of possible script injection concerns.
    li.textContent = message;
};

connection.on("Connect", messageCallback);
connection.on("Broadcast", messageCallback);

connection.on("GetMessage", async function () {
    document.getElementById("sendButton").disabled = false;
    var res = await new Promise(function (resolve, reject) {
        document.getElementById("sendButton").addEventListener("click", (event) => {
            var message = document.getElementById("messageInput").value;
            resolve(message);
        });
    });
    document.getElementById("sendButton").disabled = true;
    return res;
});

connection.start().then(function () {
    document.getElementById("getButton").disabled = false;
}).catch(function (err) {
    return console.error(err.toString());
});

document.getElementById("getButton").addEventListener("click", async function (event) {
    var id = document.getElementById("IDInput").value;
    var ret = await connection.invoke("GetMessage", id).catch(function (err) {
        return console.error(err.toString());
    });
    var li = document.createElement("li");
    document.getElementById("messagesList").appendChild(li);
    // We can assign user-supplied strings to an element's textContent because it
    // is not interpreted as markup. If you're assigning in any other way, you 
    // should be aware of possible script injection concerns.
    li.textContent = ret;
    event.preventDefault();
});

document.getElementById("broadcastButton").addEventListener("click", async function (event) {
    var message = document.getElementById("messageInput").value;
    await connection.invoke("Broadcast", message).catch(function (err) {
        return console.error(err.toString());
    });
    event.preventDefault();
});