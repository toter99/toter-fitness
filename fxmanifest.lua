fx_version 'cerulean'
game 'gta5'
author 'toter'
description 'Fitness-System mit Trainingsstationen, Minispielen, Gyms, Kursen, Personal Trainern und nativen lb-phone Apps'
version '1.0.0'
repository 'https://github.com/toter99/toter-fitness'

dependency 'oxmysql'
dependency 'qb-core'

client_scripts {
    'Client/bin/Release/*.net.dll',
    'Client/ziel.lua'
}
server_script 'Server/bin/Release/*.net.dll'

ui_page 'html/index.html'

files {
    'html/index.html',
    'html/*.css',
    'html/*.js',
    'html/telefon/*'
}
