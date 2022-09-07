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
    .withUrl("/chatHub")
    .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: function (retryContext) {
            return getRandomDelay();
        }
    })
    .build();

//Disable the send button until connection is established.
document.getElementById("sendButton").disabled = true;

connection.on("ReceiveMessage", function (user, message) {
    var li = document.createElement("li");
    document.getElementById("messagesList").appendChild(li);
    // We can assign user-supplied strings to an element's textContent because it
    // is not interpreted as markup. If you're assigning in any other way, you 
    // should be aware of possible script injection concerns.
    li.textContent = `${user} says ${message}`;
});

async function startConnection() {
    let count = 0;
    do {
        try {
            count++;
            console.log(`Attempt ${count}`);
            await connection.start();
            document.getElementById("sendButton").disabled = false;
            break;
        } catch (err) {
            await delay(getRandomDelay());
            console.error(err.toString());
        }
    } while (true);
}

startConnection();

document.getElementById("sendButton").addEventListener("click", function (event) {
    var user = document.getElementById("userInput").value;
    var message = document.getElementById("messageInput").value;
    connection.invoke("SendMessage", user, message).catch(function (err) {
        return console.error(err.toString());
    });
    event.preventDefault();
});