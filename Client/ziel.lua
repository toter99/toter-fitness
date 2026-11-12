-- Alt-Menü (ox_target, sonst qb-target): "Coachen" am anderen Spieler.
-- In Lua, weil ox_target und qb-target ihre Funktionen (canInteract, onSelect) direkt aufrufen,
-- das klappt aus C# heraus nicht zuverlässig. Ob man coachen darf, schickt der C#-Teil per Event.

local darf = false

AddEventHandler('toter-fitness:client:zielStatus', function(wert)
    darf = wert == true
end)

local function coachen(ped)
    if not ped or ped == 0 then return end
    local spieler = NetworkGetPlayerIndexFromPed(ped)
    if spieler == -1 then return end
    TriggerServerEvent('toter-fitness:server:coachen', GetPlayerServerId(spieler))
end

local function anmelden()
    if GetResourceState('ox_target') == 'started' then
        exports.ox_target:removeGlobalPlayer('toter_fitness_coachen')
        exports.ox_target:addGlobalPlayer({
            {
                name = 'toter_fitness_coachen',
                label = 'Coachen',
                icon = 'fas fa-dumbbell',
                distance = 2.5,
                canInteract = function(entity) return darf and entity ~= PlayerPedId() end,
                onSelect = function(data) coachen(data.entity) end,
            },
        })
    elseif GetResourceState('qb-target') == 'started' then
        exports['qb-target']:RemoveGlobalPlayer('Coachen')
        exports['qb-target']:AddGlobalPlayer({
            options = {
                {
                    icon = 'fas fa-dumbbell',
                    label = 'Coachen',
                    canInteract = function(entity) return darf and entity ~= PlayerPedId() end,
                    action = function(entity) coachen(entity) end,
                },
            },
            distance = 2.5,
        })
    end
end

CreateThread(function()
    Wait(1000)
    anmelden()
end)

AddEventHandler('onClientResourceStart', function(name)
    if name == 'ox_target' or name == 'qb-target' then
        Wait(500)
        anmelden()
    end
end)

AddEventHandler('onResourceStop', function(name)
    if name ~= GetCurrentResourceName() then return end
    if GetResourceState('ox_target') == 'started' then
        exports.ox_target:removeGlobalPlayer('toter_fitness_coachen')
    elseif GetResourceState('qb-target') == 'started' then
        exports['qb-target']:RemoveGlobalPlayer('Coachen')
    end
end)
