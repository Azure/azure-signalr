"use strict";

function getRandomDelay() {
    function getRandom(min, max) {
        return Math.floor(Math.random() * (max - min + 1) + min);
    }
    return getRandom(1000, 2000);
}

function delay(time) {
    return new Promise((resolve) => {
        setTimeout(() => resolve(), time);
    });
}

var connection = new signalR.HubConnectionBuilder()
    .withUrl("/chat")
    .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: function (retryContext) {
            return getRandomDelay();
        }
    })
    .build();

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

async function startConnection() {
    let count = 0;
    do {
        try {
            count++;
            console.log(`Attempt ${count}`);
            await connection.start();
            document.getElementById("getButton").disabled = false;
            break;
        } catch (err) {
            await delay(getRandomDelay());
            console.error(err.toString());
        }
    } while (true);
}

startConnection();

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