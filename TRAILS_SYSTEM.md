# Sistema de Trails para CS2

## Plugin Trails Recomendado

Para adicionar trails (partículas/efeitos visuais que seguem os jogadores), recomendamos usar o plugin oficial:

**Trails Plugin do SharpTimer:**
- Repositório: https://github.com/SharpTimer/Trails
- Compatível com CounterStrikeSharp
- Permite criar trilhas personalizadas para jogadores
- Configurável por cores, efeitos e condições

## Como Instalar

1. Baixe o plugin do repositório: https://github.com/SharpTimer/Trails/releases
2. Extraia para: `custom_files/addons/counterstrikesharp/plugins/Trails/`
3. Configure o arquivo de configuração gerado após a primeira execução
4. Reinicie o servidor

## Alternativa: Criar Plugin Customizado

Se preferir criar um plugin customizado integrado com o sistema de ranking:

- Use eventos `EventPlayerSpawn` e `OnTick` para rastrear posições
- Crie entidades de partículas usando a API do CounterStrikeSharp
- Integre com o sistema de níveis (trails diferentes por nível)
- Salve preferências em JSON

## Nota

O sistema de trails requer conhecimento avançado da API do CounterStrikeSharp e criação de entidades visuais no jogo. 
Recomendamos usar o plugin existente do SharpTimer para uma solução mais estável.

