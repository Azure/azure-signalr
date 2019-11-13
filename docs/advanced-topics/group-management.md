# Group Management

A group is a collection of connections/users associated with a name. Messages can be sent to all connections/users in a group. Groups are the recommended way to send to a connection/user or multiple connections/users because the groups are managed by the application. A connection/user can be a member of multiple groups. 

> User ID: can be used for multiple connections
>
> Connection ID: unique, one ID can be used for only one connection 

## Content

- [Group Management](#group-management)
  - [Content](#content)
  - [Group management for connection ID](#group-management-for-connection-id)
    - [Adding and removing connection IDs](#adding-and-removing-connection-ids)
      - [Usage](#usage)
  - [Group management for user ID](#group-management-for-user-id)
    - [Adding and removing user IDs](#adding-and-removing-user-ids)
      - [Usage](#usage-1)
    - [Remove user from all groups](#remove-user-from-all-groups)
    - [Check user existence in a group](#check-user-existence-in-a-group)
      - [Usage](#usage-2)
  - [Typical scenarios](#typical-scenarios)
    - [App Server Environmwent](#app-server-environmwent)
      - [Server Side](#server-side)
      - [Client Side](#client-side)
    - [Serverless Environment](#serverless-environment)
      - [Function Side](#function-side)
      - [Client Side](#client-side-1)

SignalR Service allows users/connections to be added/removed to/from groups.

In general, we support 5 methods for group management for all SDK and REST API for Azure SignalR Service:

|  | [Server SDK](https://www.nuget.org/packages/Microsoft.Azure.SignalR/) | [Management SDK](https://www.nuget.org/packages/Microsoft.Azure.SignalR.Management) | [Function Binding](https://www.nuget.org/packages/Microsoft.Azure.WebJobs.Extensions.SignalRService) | [REST API](https://github.com/Azure/azure-signalr/blob/dev/docs/rest-api.md) |
| --- | --- | --- | --- | --- |
| Add a connection ID to a group | :heavy_check_mark: |  :heavy_check_mark: | :heavy_check_mark: | :heavy_check_mark: |
| Remove a connection ID from a group | :heavy_check_mark: | :heavy_check_mark: | :heavy_check_mark: | :heavy_check_mark: |
| Add a user ID from a group | `N/A` | :heavy_check_mark: | :heavy_check_mark: | :heavy_check_mark: |
| Remove a user ID from a group | `N/A` | :heavy_check_mark: | :heavy_check_mark: | :heavy_check_mark: |
| Remove a user ID from all groups | `N/A` |  :heavy_check_mark: | :heavy_check_mark: | :heavy_check_mark: |
| Check user existence in a group | `N/A` |  `N/A` | `N/A` | :heavy_check_mark: |

## Group management for connection ID

To add or remove connection IDs from a group, you call the add or remove methods, and pass in the connection id and group's name as parameters. Group membership **IS NOT** preserved when a connection ends. The connection needs to rejoin the group when it's re-established.

At the momment, Azure SignalR Service doesn't provide any methods to check connection membership in a group. Therefore, it is app server's responsibility to manage the connection membership.

### Adding and removing connection IDs

#### Usage

* [Server SDK Usage](https://docs.microsoft.com/en-us/aspnet/core/signalr/groups?view=aspnetcore-3.0#groups-in-signalr)
* [Management SDK Usage](../management-sdk-guide.md#iservicehubcontext)
* [Function Binding Usage](https://github.com/Azure/azure-functions-signalrservice-extension#using-the-signalr-output-binding)
* [REST API Usage](../rest-api.md)

## Group management for user ID

To add or remove user IDs from a group, you call the add or remove methods, and pass in the user ID and group's name as parameters. Unlike connection ID, Group membership **IS** preserved when a user disconnects.

Note that user ID can be viewed as a **tag** to one or more connections. If you add a new tag to a group, that means you notify the group that all the group messages should be sent to the added tag. So it is your responsibility to update the tags (user IDs) in a group.

We provides 2 options to clear tags in groups:

1. [Add a user ID to a group with `ttl`](#adding-and-removing-user-ids).
2. [Excipitly remove a user ID from all groups](#remove-user-from-all-groups).

For how to choose the clear method, please refer to [todo].

### Adding and removing user IDs

As for adding a user ID to group, you can specify a TTL, which mean the user ID will be automatically removed for the specific group. If you add user ID to the specific group for multiple times, the TTL will be update the latest TTL. To remove the specific user ID from all groups, please see [Remove user from all groups](#remove-user-from-all-groups) section.

#### Usage

* [Management SDK Usage](../management-sdk-guide.md#iservicehubcontext)
* [Function Binding Usage](https://github.com/Azure/azure-functions-signalrservice-extension#using-the-signalr-output-binding)
* [REST API Usage](../rest-api.md#add-a-user-to-a-group)

### Remove user from all groups

Unlike adding a user ID to a group with TTL, you don't have to decide TTL to user-group pair at the time you add a user ID to a group. Removing a user from all groups can be invoke at any time you want. It will remove the specific user ID in all groups. This is useful when you disconnect all the connections with the specific user ID and want no groups joined after reconnect.  

* [Management SDK Usage](../management-sdk-guide.md#iservicehubcontext)
* [Function Binding Usage](https://github.com/Azure/azure-functions-signalrservice-extension#using-the-signalr-output-binding)
* [REST API Usage](../rest-api.md#remove-a-user-from-all-groups)

### Check user existence in a group

// talk about why we need this

* [REST API Usage](../rest-api.md#check-user-existence-in-a-group)

#### Usage

* [Management SDK Usage](https://github.com/Azure/azure-signalr/blob/dev/docs/management-sdk-guide.md#iservicehubcontext)
* [Function Binding Usage](https://github.com/Azure/azure-functions-signalrservice-extension#using-the-signalr-output-binding)
* [REST API Usage](../rest-api.md#remove-user-from-all-groups)

## Typical scenarios

### App Server Environmwent

// todo: simple

#### Server Side

#### Client Side

### Serverless Environment

// todo: user and group, ttl, clear

#### Function Side

#### Client Side