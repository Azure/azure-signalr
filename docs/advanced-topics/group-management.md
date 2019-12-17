# Group Management  <!-- omit in toc -->

A group is a collection of connections/users associated with a name. Messages can be sent to all connections/users in a group. Groups are the recommended way to send to a connection/user or multiple connections/users because the groups are managed by the service. A connection/user can be a member of multiple groups. 

> User ID: can be used for multiple connections
>
> Connection ID: unique, one ID can be used for only one connection 

## Content  <!-- omit in toc -->

- [Group management for connection ID](#group-management-for-connection-id)
  - [Adding and removing connection IDs](#adding-and-removing-connection-ids)
    - [Usage](#usage)
- [Group management for user ID](#group-management-for-user-id)
  - [Adding user IDs](#adding-user-ids)
    - [Usage](#usage-1)
  - [Removing user IDs](#removing-user-ids)
    - [Usage](#usage-2)
  - [Remove user from all groups](#remove-user-from-all-groups)
    - [Usage](#usage-3)
  - [Check user existence in a group](#check-user-existence-in-a-group)
    - [Usage](#usage-4)

SignalR Service allows users or connections to be added to or removed from groups.

In general, we support 5 methods for group management for all SDKs and REST API for Azure SignalR Service:

|  | [Server SDK](https://www.nuget.org/packages/Microsoft.Azure.SignalR/) | [Management SDK](https://www.nuget.org/packages/Microsoft.Azure.SignalR.Management) | [Function Binding](https://www.nuget.org/packages/Microsoft.Azure.WebJobs.Extensions.SignalRService) | [REST API](https://github.com/Azure/azure-signalr/blob/dev/docs/rest-api.md) |
| --- | --- | --- | --- | --- |
| Add a connection ID to a group | :heavy_check_mark: |  :heavy_check_mark: | :heavy_check_mark: | :heavy_check_mark: |
| Remove a connection ID from a group | :heavy_check_mark: | :heavy_check_mark: | :heavy_check_mark: | :heavy_check_mark: |
| Add a user ID to a group | `N/A` | :heavy_check_mark: | :heavy_check_mark: | :heavy_check_mark: |
| Remove a user ID from a group | `N/A` | :heavy_check_mark: | :heavy_check_mark: | :heavy_check_mark: |
| Remove a user ID from all groups | `N/A` |  :heavy_check_mark: | :heavy_check_mark: | :heavy_check_mark: |
| Check user ID existence in a group | `N/A` |  `N/A` | `N/A` | :heavy_check_mark: |

## Group management for connection ID

To add or remove connection IDs from a group, you call the add or remove methods, and pass in the connection ID and group's name as parameters. Group membership is **NOT** preserved when a connection ends. The connection needs to rejoin the group when it's re-established.

At the momment, Azure SignalR Service doesn't provide any methods to check connection membership in a group. Therefore, it is app server's responsibility to manage the connection membership.

### Adding and removing connection IDs

#### Usage

* [Server SDK Usage](https://docs.microsoft.com/en-us/aspnet/core/signalr/groups?view=aspnetcore-3.0#groups-in-signalr)
* [Management SDK Usage](../management-sdk-guide.md#iservicehubcontext)
* [Function Binding Usage](https://github.com/Azure/azure-functions-signalrservice-extension#using-the-signalr-output-binding)
* [REST API Usage](../rest-api.md)

## Group management for user ID

To add or remove user IDs from a group, you call the add or remove methods, and pass in the user ID and group's name as parameters. Unlike connection ID, User-group membership **IS** preserved when a conenction of the user ID disconnects by default. For how to manipulate user-group membership 

Note that user ID can be viewed as a **tag** to one or more connections. If you add a new tag to a group, that means you notify the group that all the group messages should be sent to the connetion with the tag. It is your responsibility to update the tags (user IDs) in a group.

We provides 2 options to clear tags in groups: [Removing user IDs](#removing-user-ids) and [Remove a user ID from all groups](#remove-user-from-all-groups).

### Adding user IDs

TTL determines whether the **future** connections with the user ID is added to the group. To remove the alrealy existing connections with the user ID from a group excipitly, you can call `remove a user ID from a group` API. To remove the alrealy existing connections with the user ID from all groups excipitly, please see [Remove user from all groups](#remove-user-from-all-groups) section.

Once TTL is set to a user-group pair, newly connected connections with this user ID will be added to the group automatically on or before the expiration time (update time + TTL), otherwise the connection will not added to the group. Once it is connected, the group membership to the connection will **NOT** change no matter how TTL changes, unless the membership is mamually remove by `remove a user ID from a group` or `remove a user ID from all groups` API.

> Note
> 
> 1. If the TTL is **always** set to 0, the current connection will be added to the group, while the newly connections never be added to the group. This is useful when you want to avoid preserving any user-group membership when a connection ends.
>
> 2. If you `add a user ID to a group` API multiple times, the TTL for the user-group pair will be updated to the latest one.

#### Usage

* [Management SDK Usage](../management-sdk-guide.md#iservicehubcontext)
* [Function Binding Usage](https://github.com/Azure/azure-functions-signalrservice-extension#using-the-signalr-output-binding)
* [REST API Usage](../rest-api.md#add-a-user-to-a-group)

### Removing user IDs

`remove a user ID from a group` API can be used to remove the connections with the specific user ID from a group.

#### Usage
* [Management SDK Usage](../management-sdk-guide.md#iservicehubcontext)
* [Function Binding Usage](https://github.com/Azure/azure-functions-signalrservice-extension#using-the-signalr-output-binding)
* [REST API Usage](../rest-api.md#remove-a-user-from-a-group)


### Remove user from all groups

`remove a user ID from all groups` API can be used to remove the connections with the specific user ID from all groups. This API is useful when you disconnect all the connections with the specific user ID and also want the specific user leaves all the groups joined before, so that the user won't be in any group when it reconnects. Removing a user from all groups can be invoke at any time you want. It will remove the specific user ID in all groups.

#### Usage
* [Management SDK Usage](../management-sdk-guide.md#iservicehubcontext)
* [Function Binding Usage](https://github.com/Azure/azure-functions-signalrservice-extension#using-the-signalr-output-binding)
* [REST API Usage](../rest-api.md#remove-a-user-from-all-groups)

### Check user existence in a group

Sometimes, you need to check whether a user is in a group or not before. For server environment, you are able to manage the user-group membership in your app server, but for serverless environment, you have no place to manage it. You can call "Check user existence in a group" REST API to accomplish it.

#### Usage
* [REST API Usage](../rest-api.md#check-user-existence-in-a-group)